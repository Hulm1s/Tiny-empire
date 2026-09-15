"""Builds one finished character per role: hat, colours, clips, exported FBX.

Each role gets its own copy of the body with its headwear joined in and weighted
to the head bone, so a hat is part of the skinned mesh rather than a second
object Unity would have to keep attached.
"""
import bpy, sys, os, math

BLEND = sys.argv[sys.argv.index("--") + 1]
OUT = sys.argv[sys.argv.index("--") + 2]


def srgb(r, g, b):
    def lin(c):
        c /= 255.0
        return c / 12.92 if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4
    return (lin(r), lin(g), lin(b))


bpy.ops.wm.open_mainfile(filepath=BLEND)
scene = bpy.context.scene
rig = bpy.data.objects["Rig"]
base = bpy.data.objects["Body"]

# Where the head actually is, so hats sit on it rather than near it.
head_verts = [v.co for v in base.data.vertices if v.co.z > 1.14]
head_top = max(v.z for v in head_verts)
head_half = max(abs(v.x) for v in head_verts)
head_y = sum(v.y for v in head_verts) / len(head_verts)
print(f"HEAD top={head_top:.3f} half={head_half:.3f} y={head_y:.3f}")

FRONT = -1.0


def mat(name, rgb):
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    b = m.node_tree.nodes["Principled BSDF"]
    b.inputs["Base Color"].default_value = (*rgb, 1.0)
    b.inputs["Roughness"].default_value = 0.85
    b.inputs["Specular"].default_value = 0.08
    return m


def cyl(r, depth, loc, verts=10, scale=(1, 1, 1)):
    bpy.ops.mesh.primitive_cylinder_add(vertices=verts, radius=r, depth=depth, location=loc)
    o = bpy.context.active_object
    o.scale = scale
    return o


def ball(r, loc, scale=(1, 1, 1)):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=10, ring_count=5, radius=r, location=loc)
    o = bpy.context.active_object
    o.scale = scale
    return o


def box(scale, loc, rot=(0, 0, 0)):
    bpy.ops.mesh.primitive_cube_add(size=1, location=loc, rotation=rot)
    o = bpy.context.active_object
    o.scale = scale
    return o


def straw_hat():
    """Wide brim and a low crown. The brim is what reads at game size."""
    brim = cyl(head_half * 2.15, 0.022, (0, head_y, head_top - 0.055))
    crown = cyl(head_half * 1.02, 0.11, (0, head_y, head_top + 0.01))
    band = cyl(head_half * 1.06, 0.028, (0, head_y, head_top - 0.028))
    return [brim, crown], [band]


def cap():
    """Rounded crown plus a peak tipped down at the front."""
    crown = ball(head_half * 1.06, (0, head_y, head_top - 0.035), scale=(1, 1.02, 0.62))
    peak = box((head_half * 1.55, head_half * 1.25, 0.022),
               (0, head_y + FRONT * head_half * 1.15, head_top - 0.045),
               rot=(math.radians(-10), 0, 0))
    button = ball(head_half * 0.14, (0, head_y, head_top + 0.055))
    return [crown, peak], [button]


ROLES = {
    "Farmer": dict(
        colours=[srgb(92, 62, 38), srgb(140, 104, 60), srgb(52, 120, 210), srgb(232, 180, 140)],
        hat=straw_hat, hat_colours=[srgb(214, 178, 96), srgb(150, 110, 58)]),
    "Cashier": dict(
        colours=[srgb(70, 52, 44), srgb(70, 74, 86), srgb(46, 168, 116), srgb(228, 174, 134)],
        hat=cap, hat_colours=[srgb(46, 82, 150), srgb(210, 214, 220)]),
    "Customer": dict(
        colours=[srgb(88, 62, 50), srgb(150, 132, 112), srgb(214, 92, 72), srgb(240, 190, 150)],
        hat=None, hat_colours=[]),
}

SLOTS = ("Shoe", "Trouser", "Shirt", "Skin")
built = []

for role, spec in ROLES.items():
    bpy.ops.object.select_all(action='DESELECT')
    body = base.copy()
    body.data = base.data.copy()
    body.name = f"Body_{role}"
    scene.collection.objects.link(body)

    # Recolour the four existing slots in place. Clearing them would orphan every
    # face's material index and flatten the whole body to one colour.
    for i, rgb in enumerate(spec["colours"]):
        m = body.data.materials[i].copy()
        m.name = f"{role}_{SLOTS[i]}"
        m.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = (*rgb, 1.0)
        body.data.materials[i] = m

    if spec["hat"]:
        parts, accents = spec["hat"]()
        main_mat = mat(f"{role}_Hat", spec["hat_colours"][0])
        accent_mat = mat(f"{role}_HatTrim", spec["hat_colours"][1])
        for o in parts:
            o.data.materials.append(main_mat)
        for o in accents:
            o.data.materials.append(accent_mat)

        before = len(body.data.vertices)

        bpy.ops.object.select_all(action='DESELECT')
        for o in parts + accents:
            o.select_set(True)
        body.select_set(True)
        bpy.context.view_layer.objects.active = body
        bpy.ops.object.join()

        after = len(body.data.vertices)
        added = list(range(before, after))

        # Everything joined in belongs to the head and nothing else, or the hat
        # will stay behind when the character looks around.
        for g in body.vertex_groups:
            if g.name != "Head":
                g.remove(added)
        head_group = body.vertex_groups.get("Head") or body.vertex_groups.new(name="Head")
        head_group.add(added, 1.0, 'REPLACE')
        print(f"{role}: hat joined, {len(added)} verts weighted to Head")

    bpy.ops.object.shade_flat()
    body.data.calc_loop_triangles()
    built.append((role, body, len(body.data.loop_triangles)))

# ------------------------------------------------------------------- export
for role, body, tris in built:
    bpy.ops.object.select_all(action='DESELECT')
    body.select_set(True)
    rig.select_set(True)
    bpy.context.view_layer.objects.active = rig

    path = os.path.join(OUT, f"Villager_{role}.fbx")
    bpy.ops.export_scene.fbx(
        filepath=path, use_selection=True,
        add_leaf_bones=False, bake_anim=True,
        bake_anim_use_all_actions=True, bake_anim_use_nla_strips=False,
        bake_anim_simplify_factor=0.0,
        mesh_smooth_type='FACE',
        axis_forward='-Z', axis_up='Y',
        apply_scale_options='FBX_SCALE_ALL')
    print(f"EXPORTED {role} tris={tris} fbx={os.path.getsize(path)/1024:.0f}KB")

bpy.ops.wm.save_as_mainfile(filepath=os.path.join(OUT, "villagers.blend"))
print("ROLES_OK")
