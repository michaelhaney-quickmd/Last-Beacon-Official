import bpy, os

BLEND = "/Users/michaelhaney/Projects/Last Beacon Official/Blender/Cliff_TerraceEast_A.blend"
OUT   = "/Users/michaelhaney/Projects/Last Beacon Official/Assets/_Project/Art/Environment/Rocks/Test/Cliff_TerraceEast_A.fbx"

bpy.ops.wm.open_mainfile(filepath=BLEND)
obj = bpy.data.objects["Cliff_TerraceEast_A"]
bpy.ops.object.select_all(action='DESELECT')
obj.select_set(True)
bpy.context.view_layer.objects.active = obj
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)

os.makedirs(os.path.dirname(OUT), exist_ok=True)
bpy.ops.export_scene.fbx(
    filepath=OUT,
    use_selection=True,
    apply_unit_scale=True,
    global_scale=1.0,
    apply_scale_options='FBX_SCALE_NONE',
    axis_forward='-Z',         # Blender defaults: these perform the Z-up -> Y-up
    axis_up='Y',               # conversion Unity expects

    object_types={'MESH'},
    use_mesh_modifiers=True,
    mesh_smooth_type='FACE',
    use_tspace=False,
    add_leaf_bones=False,
    bake_anim=False,
)
print(f"[EX] exported {OUT}")
print(f"[EX] blender dims (x,y,z) = {tuple(round(d,3) for d in obj.dimensions)}")
print(f"[EX] axis_forward=-Z axis_up=Y (Blender defaults), transforms applied")
