"""Animates the rigged humanoid and colours it into role variants.

Loads the rig built by rig.py rather than rebuilding it, so the slow retopology
runs once. Clips are keyframed by hand in code - there is no motion capture here,
so they are deliberately simple and readable rather than lifelike.
"""
import bpy, sys, os, math

BLEND = sys.argv[sys.argv.index("--") + 1]
OUT = sys.argv[sys.argv.index("--") + 2]

bpy.ops.wm.open_mainfile(filepath=BLEND)

arm = bpy.data.objects["Rig"]
body = bpy.data.objects["Body"]

# Clear the test pose left behind by the rigging pass.
bpy.context.view_layer.objects.active = arm
bpy.ops.object.mode_set(mode='POSE')
bpy.ops.pose.select_all(action='SELECT')
bpy.ops.pose.transforms_clear()
for pb in arm.pose.bones:
    pb.rotation_mode = 'XYZ'
bpy.ops.object.mode_set(mode='OBJECT')


def key(frame, poses):
    """poses: {bone: (x_deg, y_deg, z_deg)} plus optional 'ROOT_Z'."""
    for name, value in poses.items():
        if name == "ROOT_Z":
            pb = arm.pose.bones["Root"]
            pb.location = (0, 0, value)
            pb.keyframe_insert("location", frame=frame)
            continue
        pb = arm.pose.bones.get(name)
        if pb is None:
            continue
        pb.rotation_euler = tuple(math.radians(d) for d in value)
        pb.keyframe_insert("rotation_euler", frame=frame)


def new_action(name):
    act = bpy.data.actions.new(name)
    act.use_fake_user = True
    arm.animation_data_create()
    arm.animation_data.action = act
    return act


def cyclic(act):
    """Make every curve loop, so Unity gets a seamless clip."""
    for fc in act.fcurves:
        m = fc.modifiers.new('CYCLES')
        m.mode_before = 'REPEAT_OFFSET'
        m.mode_after = 'REPEAT_OFFSET'


# X is the swing axis on every limb: the bones all point down, and negative X
# carries a thigh forward. Verified by posing and looking, not assumed.
FWD, BACK = -1, 1

# ----------------------------------------------------------------- idle
new_action("Idle")
for f, breathe in ((1, 0.0), (45, -0.012), (90, 0.0)):
    key(f, {
        "ROOT_Z": breathe,
        "Chest": (breathe * 90, 0, 0),
        "Head": (-breathe * 60, 0, 0),
        "UpperArm.L": (0, 0, -4), "UpperArm.R": (0, 0, 4),
        "Forearm.L": (-5, 0, 0), "Forearm.R": (-5, 0, 0),
        "Thigh.L": (0, 0, 0), "Thigh.R": (0, 0, 0),
        "Shin.L": (0, 0, 0), "Shin.R": (0, 0, 0),
    })
cyclic(bpy.data.actions["Idle"])


def walk_clip(name, arms_up=False):
    new_action(name)
    # Four keys per stride: contact, pass, contact mirrored, pass mirrored.
    steps = [
        (1,  dict(tl=FWD * 26, sl=6,  tr=BACK * 20, sr=28, al=BACK * 34, ar=FWD * 34, bob=0.0)),
        (7,  dict(tl=FWD * 4,  sl=22, tr=FWD * 2,   sr=8,  al=BACK * 12, ar=FWD * 12, bob=0.022)),
        (13, dict(tl=BACK * 20, sl=28, tr=FWD * 26, sr=6,  al=FWD * 34,  ar=BACK * 34, bob=0.0)),
        (19, dict(tl=FWD * 2,  sl=8,  tr=FWD * 4,   sr=22, al=FWD * 12,  ar=BACK * 12, bob=0.022)),
        (25, dict(tl=FWD * 26, sl=6,  tr=BACK * 20, sr=28, al=BACK * 34, ar=FWD * 34, bob=0.0)),
    ]
    for f, s in steps:
        pose = {
            "ROOT_Z": s["bob"],
            "Thigh.L": (s["tl"], 0, 0), "Shin.L": (s["sl"], 0, 0),
            "Thigh.R": (s["tr"], 0, 0), "Shin.R": (s["sr"], 0, 0),
            "Chest": (2, 0, 0),
        }
        if arms_up:
            # Both hands up steadying the load; the legs still walk underneath.
            pose.update({
                "UpperArm.L": (-42, 0, -16), "UpperArm.R": (-42, 0, 16),
                "Forearm.L": (-88, 0, 0), "Forearm.R": (-88, 0, 0),
            })
        else:
            pose.update({
                "UpperArm.L": (s["al"], 0, -5), "UpperArm.R": (s["ar"], 0, 5),
                # Barely bent. A fixed elbow angle this shallow keeps the forearm
                # following the upper arm instead of reading as its own hinge.
                "Forearm.L": (-7, 0, 0), "Forearm.R": (-7, 0, 0),
            })
        key(f, pose)
    cyclic(bpy.data.actions[name])


walk_clip("Walk")
walk_clip("CarryWalk", arms_up=True)

# ------------------------------------------------------------- carry idle
new_action("CarryIdle")
for f, breathe in ((1, 0.0), (45, -0.010), (90, 0.0)):
    key(f, {
        "ROOT_Z": breathe,
        "UpperArm.L": (-42, 0, -16), "UpperArm.R": (-42, 0, 16),
        "Forearm.L": (-88, 0, 0), "Forearm.R": (-88, 0, 0),
        "Thigh.L": (0, 0, 0), "Thigh.R": (0, 0, 0),
        "Shin.L": (0, 0, 0), "Shin.R": (0, 0, 0),
        "Chest": (breathe * 90, 0, 0),
    })
cyclic(bpy.data.actions["CarryIdle"])

print("ACTIONS " + ", ".join(a.name for a in bpy.data.actions))


# ------------------------------------------------- colour the body by region
def make(name, rgb):
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    b = m.node_tree.nodes["Principled BSDF"]
    b.inputs["Base Color"].default_value = (*rgb, 1.0)
    b.inputs["Roughness"].default_value = 0.85
    b.inputs["Specular"].default_value = 0.08
    return m


# Measured off the rig: crotch ~0.50, shoulders ~0.91, neck ~1.19.
SHOE, TROUSER, SHIRT, SKIN = 0, 1, 2, 3


def region_of(centre):
    z, x = centre.z, abs(centre.x)
    # Hands first: they sit at the same height as the trousers, so a height-only
    # test paints them like clothing.
    if x > 0.30 and 0.44 < z < 0.74:
        return SKIN
    if z < 0.13:
        return SHOE
    # Waist, not crotch. Ending the trousers at the measured crotch left the hips
    # shirt-coloured and the whole thing read as a onesie.
    if z < 0.70:
        return TROUSER
    if z < 1.16:
        return SHIRT
    return SKIN


def srgb(r, g, b):
    """Convert an ordinary 0-255 colour to the linear values Blender expects.

    Base Color is a linear input. Typing sRGB-looking numbers straight into it -
    which is the obvious thing to do - silently washes every colour out, because
    mid sRGB is much darker in linear space than the same number suggests.
    """
    def lin(c):
        c /= 255.0
        return c / 12.92 if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4
    return (lin(r), lin(g), lin(b))


PALETTE = {
    "Farmer":   [srgb(92, 62, 38), srgb(140, 104, 60), srgb(52, 120, 210), srgb(232, 180, 140)],
    "Cashier":  [srgb(70, 52, 44), srgb(70, 74, 86), srgb(46, 168, 116), srgb(228, 174, 134)],
    "Customer": [srgb(88, 62, 50), srgb(150, 132, 112), srgb(214, 92, 72), srgb(240, 190, 150)],
}
ROLES = PALETTE

# Assign the region index per face once; the slots differ per role, the map does not.
mesh = body.data
mesh.materials.clear()
for slot_name in ("Shoe", "Trouser", "Shirt", "Skin"):
    mesh.materials.append(make(f"Farmer_{slot_name}", ROLES["Farmer"][len(mesh.materials)]))

for poly in mesh.polygons:
    poly.material_index = region_of(poly.center)

counts = {}
for poly in mesh.polygons:
    counts[poly.material_index] = counts.get(poly.material_index, 0) + 1
print(f"REGIONS shoe={counts.get(0,0)} trouser={counts.get(1,0)} "
      f"shirt={counts.get(2,0)} skin={counts.get(3,0)}")

bpy.ops.wm.save_as_mainfile(filepath=os.path.join(OUT, "humanoid_animated.blend"))
print("ANIMATE_OK")
