"""Approval render for the finished villagers: the three roles, and the walk."""
import bpy, sys, os, math

BLEND = sys.argv[sys.argv.index("--") + 1]
OUT = sys.argv[sys.argv.index("--") + 2]

bpy.ops.wm.open_mainfile(filepath=BLEND)
scene = bpy.context.scene
rig = bpy.data.objects["Rig"]

scene.render.engine = 'BLENDER_EEVEE'
scene.view_settings.view_transform = 'Standard'
scene.eevee.taa_render_samples = 24

scene.world = bpy.data.worlds.new("W")
scene.world.use_nodes = True
scene.world.node_tree.nodes["Background"].inputs[0].default_value = (0.40, 0.41, 0.48, 1)
scene.world.node_tree.nodes["Background"].inputs[1].default_value = 0.55

for name, loc, e in (("Key", (2.6, -3.4, 3.8), 230), ("Fill", (-3.2, -2.6, 1.8), 55),
                     ("Rim", (-1.4, 3.0, 3.0), 110)):
    d = bpy.data.lights.new(name, 'AREA'); d.energy = e; d.size = 4
    o = bpy.data.objects.new(name, d); o.location = loc
    scene.collection.objects.link(o)
    o.rotation_euler = (math.radians(55), 0, math.radians(35))

bpy.ops.mesh.primitive_plane_add(size=30, location=(0, 0, 0))
gm = bpy.data.materials.new("Ground")
gm.use_nodes = True
gm.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = (0.09, 0.10, 0.12, 1)
bpy.context.active_object.data.materials.append(gm)

cd = bpy.data.cameras.new("C"); cd.lens = 62
cam = bpy.data.objects.new("C", cd); scene.collection.objects.link(cam); scene.camera = cam


def aim(distance, height, angle_deg, look_z):
    a = math.radians(angle_deg)
    cam.location = (math.sin(a) * distance, -math.cos(a) * distance, height)
    cam.rotation_euler = (math.radians(math.degrees(math.atan2(distance, height - look_z))), 0, a)


roles = [bpy.data.objects[f"Body_{r}"] for r in ("Farmer", "Cashier", "Customer")]
base = bpy.data.objects["Body"]
base.hide_render = True

# --------------------------------------------------- the three roles, at rest
rig.animation_data.action = bpy.data.actions["Idle"]
scene.frame_set(1)
for i, o in enumerate(roles):
    o.location.x = (i - 1) * 0.95
    o.hide_render = False

scene.render.resolution_x, scene.render.resolution_y = 860, 640
scene.render.image_settings.file_format = 'PNG'
aim(4.4, 1.15, 18, 0.78)
scene.render.filepath = os.path.join(OUT, "roles_hats.png")
bpy.ops.render.render(write_still=True)

# ------------------------------------------------------- the walk, on the farmer
for o in roles[1:]:
    o.hide_render = True
roles[0].location.x = 0

scene.render.resolution_x, scene.render.resolution_y = 420, 560
scene.render.fps = 24
scene.render.image_settings.file_format = 'FFMPEG'
scene.render.ffmpeg.format = 'MPEG4'
scene.render.ffmpeg.codec = 'H264'
scene.render.ffmpeg.constant_rate_factor = 'HIGH'
aim(3.4, 1.05, 28, 0.74)

for clip, length in (("Walk", 72), ("CarryWalk", 72)):
    rig.animation_data.action = bpy.data.actions[clip]
    scene.frame_start, scene.frame_end = 1, length
    scene.render.filepath = os.path.join(OUT, f"role_{clip}")
    bpy.ops.render.render(animation=True)
    print(f"RENDERED {clip}")

# ------------------------------------------- side view, to judge the arm swing
scene.render.image_settings.file_format = 'PNG'
scene.render.resolution_x, scene.render.resolution_y = 380, 560
rig.animation_data.action = bpy.data.actions["Walk"]
aim(3.4, 1.05, 90, 0.74)
for frame in (1, 7, 13):
    scene.frame_set(frame)
    scene.render.filepath = os.path.join(OUT, f"swing_f{frame}.png")
    bpy.ops.render.render(write_still=True)

print("PREVIEW_ROLES_OK")
