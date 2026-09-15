"""Exports the villager set for Unity, with the rig and clips shared.

One file carries the skeleton and every animation; the role files carry only a
skinned mesh on the identical skeleton. Unity then points all of them at one
avatar and one animator controller, so adding a role costs a mesh and nothing
else - no duplicated clips, no second controller to keep in sync.
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

head_verts = [v.co for v in base.data.vertices if v.co.z > 1.14]
head_top = max(v.z for v in head_verts)
head_half = max(abs(v.x) for v in head_verts)
head_y = sum(v.y for v in head_verts) / len(head_verts)
FRONT = -1.0


def mat(name, rgb):
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    b = m.node_tree.nodes["Principled BSDF"]
    b.inputs["Base Color"].default_value = (*rgb, 1.0)
    b.inputs["Roughness"].default_value = 0.85
    b.inputs["Specular"].default_value = 0.08
    return m


def cyl(r, depth, loc, verts=10):
    bpy.ops.mesh.primitive_cylinder_add(vertices=verts, radius=r, depth=depth, location=loc)
    return bpy.context.active_object


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
    return ([cyl(head_half * 2.15, 0.022, (0, head_y, head_top - 0.055)),
             cyl(head_half * 1.02, 0.11, (0, head_y, head_top + 0.01))],
            [cyl(head_half * 1.06, 0.028, (0, head_y, head_top - 0.028))])


def cap():
    return ([ball(head_half * 1.06, (0, head_y, head_top - 0.035), (1, 1.02, 0.62)),
             box((head_half * 1.55, head_half * 1.25, 0.022),
                 (0, head_y + FRONT * head_half * 1.15, head_top - 0.045),
                 (math.radians(-10), 0, 0))],
            [ball(head_half * 0.14, (0, head_y, head_top + 0.055))])


def flat_cap():
    """Owner's cap: same crown, longer flatter peak, so it reads as the boss."""
    return ([ball(head_half * 1.08, (0, head_y, head_top - 0.045), (1.02, 1.06, 0.5)),
             box((head_half * 1.7, head_half * 1.5, 0.02),
                 (0, head_y + FRONT * head_half * 1.3, head_top - 0.055),
                 (math.radians(-4), 0, 0))],
            [box((head_half * 1.9, head_half * 0.5, 0.05),
                 (0, head_y + FRONT * head_half * 0.55, head_top - 0.04))])


# Shoe, trousers, shirt, skin - matching the material slots on the mesh.
ROLES = {
    # The owner is the one the player controls, so it gets the loudest silhouette.
    "Owner":    dict(colours=[srgb(58, 44, 36), srgb(44, 52, 74), srgb(238, 232, 218), srgb(234, 182, 142)],
                     hat=flat_cap, hat_colours=[srgb(176, 58, 46), srgb(140, 42, 34)]),
    "Farmer":   dict(colours=[srgb(92, 62, 38), srgb(140, 104, 60), srgb(52, 120, 210), srgb(232, 180, 140)],
                     hat=straw_hat, hat_colours=[srgb(214, 178, 96), srgb(150, 110, 58)]),
    "Cashier":  dict(colours=[srgb(70, 52, 44), srgb(70, 74, 86), srgb(46, 168, 116), srgb(228, 174, 134)],
                     hat=cap, hat_colours=[srgb(46, 82, 150), srgb(210, 214, 220)]),
    # Customers get tinted at runtime, so this is only the base look.
    "Customer": dict(colours=[srgb(88, 62, 50), srgb(150, 132, 112), srgb(214, 92, 72), srgb(240, 190, 150)],
                     hat=None, hat_colours=[]),
}

SLOTS = ("Shoe", "Trouser", "Shirt", "Skin")
FBX = dict(add_leaf_bones=False, mesh_smooth_type='FACE',
           axis_forward='-Z', axis_up='Y', apply_scale_options='FBX_SCALE_ALL')

built = {}
for role, spec in ROLES.items():
    bpy.ops.object.select_all(action='DESELECT')
    body = base.copy()
    body.data = base.data.copy()
    body.name = role
    scene.collection.objects.link(body)

    for i, rgb in enumerate(spec["colours"]):
        m = body.data.materials[i].copy()
        m.name = f"{role}_{SLOTS[i]}"
        m.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = (*rgb, 1.0)
        body.data.materials[i] = m

    if spec["hat"]:
        parts, accents = spec["hat"]()
        main = mat(f"{role}_Hat", spec["hat_colours"][0])
        trim = mat(f"{role}_HatTrim", spec["hat_colours"][1])
        for o in parts:
            o.data.materials.append(main)
        for o in accents:
            o.data.materials.append(trim)

        before = len(body.data.vertices)
        bpy.ops.object.select_all(action='DESELECT')
        for o in parts + accents:
            o.select_set(True)
        body.select_set(True)
        bpy.context.view_layer.objects.active = body
        bpy.ops.object.join()

        added = list(range(before, len(body.data.vertices)))
        for g in body.vertex_groups:
            if g.name != "Head":
                g.remove(added)
        (body.vertex_groups.get("Head") or body.vertex_groups.new(name="Head")).add(
            added, 1.0, 'REPLACE')

    bpy.ops.object.shade_flat()
    body.data.calc_loop_triangles()
    built[role] = (body, len(body.data.loop_triangles))

base.hide_viewport = True

# Back to the rest pose before anything is exported.
#
# Whatever action happened to be assigned gets baked into the mesh files as their
# bind pose. The last one built was CarryIdle, so every villager shipped standing
# with its arms raised - and with the animator briefly playing nothing, that pose
# was all you ever saw.
rig.animation_data.action = None
bpy.context.view_layer.objects.active = rig
bpy.ops.object.mode_set(mode='POSE')
bpy.ops.pose.select_all(action='SELECT')
bpy.ops.pose.transforms_clear()
bpy.ops.object.mode_set(mode='OBJECT')
bpy.context.scene.frame_set(0)

# --------------------------------------------- the shared rig and its clips
# Exported with the plainest mesh; Unity only needs the skeleton and the takes
# from this file, and every role file matches its bone hierarchy exactly.
bpy.ops.object.select_all(action='DESELECT')
built["Customer"][0].select_set(True)
rig.select_set(True)
bpy.context.view_layer.objects.active = rig
path = os.path.join(OUT, "Villager_Rig.fbx")
bpy.ops.export_scene.fbx(filepath=path, use_selection=True, bake_anim=True,
                         bake_anim_use_all_actions=True, bake_anim_use_nla_strips=False,
                         bake_anim_simplify_factor=0.0, **FBX)
print(f"EXPORTED Villager_Rig (skeleton + {len(bpy.data.actions)} clips) "
      f"{os.path.getsize(path)/1024:.0f}KB")

# ------------------------------------- one mesh per role, no animation inside
for role, (body, tris) in built.items():
    bpy.ops.object.select_all(action='DESELECT')
    body.select_set(True)
    rig.select_set(True)
    bpy.context.view_layer.objects.active = rig
    path = os.path.join(OUT, f"Villager_{role}.fbx")
    bpy.ops.export_scene.fbx(filepath=path, use_selection=True, bake_anim=False, **FBX)
    print(f"EXPORTED Villager_{role} tris={tris} {os.path.getsize(path)/1024:.0f}KB")

print("EXPORT_UNITY_OK")
