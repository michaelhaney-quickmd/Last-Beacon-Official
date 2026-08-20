import bpy, os
BLEND = "/Users/michaelhaney/Projects/Last Beacon Official/Blender/Cliff_TerraceEast_B.blend"
OUT   = "/Users/michaelhaney/Projects/Last Beacon Official/Assets/_Project/Art/Environment/Rocks/Test/Cliff_TerraceEast_B.fbx"
bpy.ops.wm.open_mainfile(filepath=BLEND)
ob = bpy.data.objects["Cliff_TerraceEast_B"]
bpy.ops.object.select_all(action='DESELECT'); ob.select_set(True)
bpy.context.view_layer.objects.active = ob
bpy.ops.export_scene.fbx(filepath=OUT, use_selection=True, apply_unit_scale=True,
    global_scale=1.0, apply_scale_options='FBX_SCALE_NONE',
    axis_forward='-Z', axis_up='Y', object_types={'MESH'}, use_mesh_modifiers=True,
    mesh_smooth_type='FACE', add_leaf_bones=False, bake_anim=False)
print(f"[EX] exported {OUT}")
