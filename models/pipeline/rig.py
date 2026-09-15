"""Retopologise the humanoid and fit a rig to it.

One script from the original file so there are no round trips: remesh, retopo,
scale, measure, rig. The joint positions are measured off the mesh rather than
assumed - the model's proportions are its own, and a rig built from textbook
ratios would put knees in the wrong place.
"""
import bpy, sys, os, math, mathutils

SRC = sys.argv[sys.argv.index("--") + 1]
OUT = sys.argv[sys.argv.index("--") + 2]
FACES = int(sys.argv[sys.argv.index("--") + 3])
HEIGHT = 1.5


# ------------------------------------------------------------------ build mesh
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=SRC)

body = next(o for o in bpy.data.objects if o.type == 'MESH')
bpy.context.view_layer.objects.active = body
body.select_set(True)

body.data.remesh_voxel_size = 0.012
bpy.ops.object.voxel_remesh()
bpy.ops.object.quadriflow_remesh(mode='FACES', target_faces=FACES,
                                 use_mesh_symmetry=True, smooth_normals=False)
body.modifiers.new("Tri", 'TRIANGULATE')
bpy.ops.object.modifier_apply(modifier="Tri")
bpy.ops.object.shade_flat()
body.name = "Body"

# Scale to the player's height and stand it on the floor.
zs = [v.co.z for v in body.data.vertices]
scale = HEIGHT / (max(zs) - min(zs))
body.scale = (scale, scale, scale)
bpy.ops.object.transform_apply(scale=True)
body.location.z = -min(v.co.z for v in body.data.vertices)
bpy.ops.object.transform_apply(location=True)


# --------------------------------------------------------------- measure joints
def slice_at(z, tol=0.02):
    """Vertices in a thin horizontal band, for measuring the body at that height."""
    return [v.co for v in body.data.vertices if abs(v.co.z - z) < tol]


def width_at(z):
    s = slice_at(z)
    return (max(v.x for v in s) - min(v.x for v in s)) if s else 0.0


heights = [i * HEIGHT / 120 for i in range(121)]
profile = [(z, width_at(z)) for z in heights]
max_width = max(w for _, w in profile)

# Shoulders: find the highest slice that still contains arm, by looking for
# geometry far out to the side rather than for overall width. Width alone is
# useless here - the arms hang down, so the figure is widest at the elbows, and
# a width test drops the shoulder joint halfway down the upper arm. Rotating
# from there makes a walk look like it is swinging from the elbows, which is
# exactly what it looked like.
max_half_x = max(abs(v.co.x) for v in body.data.vertices)
shoulder_z = HEIGHT * 0.72
for z, _ in reversed(profile):
    if any(abs(v.x) > max_half_x * 0.55 for v in slice_at(z, 0.02)):
        shoulder_z = z
        break
shoulder_w = width_at(shoulder_z)

# Crotch: between the legs there is nothing on the midline. Walk up from the
# floor and stop where the body closes over. Measuring gaps across the whole
# slice does not work either - the gaps between arm and torso never close.
def on_midline(z):
    return any(abs(v.x) < 0.035 for v in slice_at(z, 0.015))

crotch_z = HEIGHT * 0.45
for z in heights:
    if 0.15 < z < HEIGHT * 0.7 and on_midline(z):
        crotch_z = z
        break

# Neck: the pinch between the shoulders and the head.
neck_band = [(z, w) for z, w in profile if shoulder_z < z < HEIGHT - 0.22]
neck_z = min(neck_band, key=lambda p: p[1])[0] if neck_band else shoulder_z + 0.08

foot = [v.co for v in body.data.vertices if v.co.z < 0.06]
foot_y = min(v.y for v in foot) if foot else -0.1
arm_x = max(abs(v.co.x) for v in body.data.vertices
            if shoulder_z - 0.25 < v.co.z < shoulder_z)

# Hands are the lowest thing out at arm's width - but so are wide boots, and
# without the height floor this measures a foot and drops the elbow to the hip.
arm_side = [v.co for v in body.data.vertices
            if abs(v.co.x) > arm_x * 0.72 and v.co.z > crotch_z]
hand_z = min(v.z for v in arm_side) if arm_side else crotch_z + 0.15

print(f"MEASURED height={HEIGHT} shoulder_z={shoulder_z:.3f} shoulder_w={shoulder_w:.3f} "
      f"crotch_z={crotch_z:.3f} neck_z={neck_z:.3f} arm_x={arm_x:.3f} "
      f"hand_z={hand_z:.3f} foot_y={foot_y:.3f}")


# ------------------------------------------------------------------ the rig
bpy.ops.object.armature_add(enter_editmode=True, location=(0, 0, 0))
arm = bpy.context.active_object
arm.name = "Rig"
eb = arm.data.edit_bones
eb.remove(eb[0])   # drop the default bone

hip_z = crotch_z + (shoulder_z - crotch_z) * 0.12
chest_z = shoulder_z - 0.06
knee_z = crotch_z * 0.52
ankle_z = 0.07
elbow_z = shoulder_z - (shoulder_z - hand_z) * 0.45
leg_x = crotch_z * 0.22 + 0.02


def bone(name, head, tail, parent=None, connected=False):
    b = eb.new(name)
    b.head = mathutils.Vector(head)
    b.tail = mathutils.Vector(tail)
    if parent:
        b.parent = eb[parent]
        b.use_connect = connected
    return b


bone("Root",   (0, 0, 0),          (0, 0, hip_z * 0.5))
bone("Hips",   (0, 0, hip_z),      (0, 0, hip_z + 0.10), "Root")
bone("Spine",  (0, 0, hip_z + 0.10), (0, 0, chest_z), "Hips", True)
bone("Chest",  (0, 0, chest_z),    (0, 0, neck_z), "Spine", True)
bone("Neck",   (0, 0, neck_z),     (0, 0, neck_z + 0.06), "Chest", True)
bone("Head",   (0, 0, neck_z + 0.06), (0, 0, HEIGHT - 0.02), "Neck", True)

for side, s in (("L", 1), ("R", -1)):
    # Clavicle runs out sideways; the upper arm then hangs almost straight down.
    # Keeping the upper arm vertical is what makes a shoulder swing read as a
    # shoulder swing - a diagonal bone pivots the whole arm sideways instead.
    sx = s * arm_x * 0.80
    bone(f"Shoulder.{side}", (s * 0.04, 0, shoulder_z - 0.02), (sx, 0, shoulder_z - 0.04), "Chest")
    bone(f"UpperArm.{side}", (sx, 0, shoulder_z - 0.04), (s * arm_x * 0.90, 0, elbow_z),
         f"Shoulder.{side}", True)
    bone(f"Forearm.{side}",  (s * arm_x * 0.90, 0, elbow_z),
         (s * arm_x * 0.97, 0, hand_z + 0.05), f"UpperArm.{side}", True)
    bone(f"Hand.{side}",     (s * arm_x * 0.97, 0, hand_z + 0.05), (s * arm_x * 0.97, 0, hand_z),
         f"Forearm.{side}", True)

    bone(f"Thigh.{side}", (s * leg_x, 0, hip_z), (s * leg_x, 0, knee_z), "Hips")
    bone(f"Shin.{side}",  (s * leg_x, 0, knee_z), (s * leg_x, 0, ankle_z),
         f"Thigh.{side}", True)
    bone(f"Foot.{side}",  (s * leg_x, 0, ankle_z), (s * leg_x, foot_y, 0.01),
         f"Shin.{side}", True)

bpy.ops.object.mode_set(mode='OBJECT')
print(f"RIG bones={len(arm.data.bones)}")

# ------------------------------------------------------- bind the mesh to it
bpy.ops.object.select_all(action='DESELECT')
body.select_set(True)
arm.select_set(True)
bpy.context.view_layer.objects.active = arm
try:
    bpy.ops.object.parent_set(type='ARMATURE_AUTO')
    print("WEIGHTS ok (automatic)")
except RuntimeError as e:
    print(f"WEIGHTS_FAILED {e}")

# ------------------------------------------- a test pose, to prove it deforms
bpy.context.view_layer.objects.active = arm
bpy.ops.object.mode_set(mode='POSE')
for name, axis, deg in (("Thigh.L", 0, -35), ("Shin.L", 0, 45),
                        ("Thigh.R", 0, 25), ("UpperArm.L", 0, 30),
                        ("UpperArm.R", 0, -30), ("Forearm.L", 0, -40),
                        ("Head", 0, -12)):
    pb = arm.pose.bones.get(name)
    if pb:
        pb.rotation_mode = 'XYZ'
        r = list(pb.rotation_euler)
        r[axis] = math.radians(deg)
        pb.rotation_euler = r
bpy.ops.object.mode_set(mode='OBJECT')

# ----------------------------------------------------------------- previews
scene = bpy.context.scene
scene.render.engine = 'BLENDER_EEVEE'
scene.view_settings.view_transform = 'Standard'
scene.render.image_settings.file_format = 'PNG'
scene.render.resolution_x, scene.render.resolution_y = 420, 640

mat = bpy.data.materials.new("Body")
mat.use_nodes = True
mat.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = (0.78, 0.60, 0.46, 1)
mat.node_tree.nodes["Principled BSDF"].inputs["Roughness"].default_value = 0.85
body.data.materials.clear()
body.data.materials.append(mat)

scene.world = bpy.data.worlds.new("W")
scene.world.use_nodes = True
scene.world.node_tree.nodes["Background"].inputs[0].default_value = (0.40, 0.40, 0.47, 1)
scene.world.node_tree.nodes["Background"].inputs[1].default_value = 0.8
for name, loc, e in (("Key", (2.4, -3.2, 3.6), 300), ("Fill", (-3, -2.4, 1.8), 80)):
    d = bpy.data.lights.new(name, 'AREA'); d.energy = e; d.size = 4
    o = bpy.data.objects.new(name, d); o.location = loc
    scene.collection.objects.link(o)
    o.rotation_euler = (math.radians(55), 0, math.radians(35))

cd = bpy.data.cameras.new("C"); cd.lens = 60
cam = bpy.data.objects.new("C", cd); scene.collection.objects.link(cam); scene.camera = cam
a = math.radians(30); r = 3.6
cam.location = (math.sin(a) * r, -math.cos(a) * r, 1.0)
cam.rotation_euler = (math.radians(84), 0, a)
scene.render.filepath = os.path.join(OUT, "rig_testpose.png")
bpy.ops.render.render(write_still=True)

bpy.ops.wm.save_as_mainfile(filepath=os.path.join(OUT, "humanoid_rigged.blend"))
print("RIG_OK")
