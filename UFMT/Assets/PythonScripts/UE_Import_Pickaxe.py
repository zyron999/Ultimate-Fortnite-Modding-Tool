import unreal; unreal.AssetRegistryHelpers.get_asset_registry().scan_paths_synchronous(['/Game'])
import os
import unreal
import json
import sys

json_path = os.environ.get("UFMT_JSON_PATH")

if not json_path or not os.path.exists(json_path):
    unreal.log_error("Critical Error: JSON data path was not found in environment variables.")
    sys.exit(1)

with open(json_path, 'r') as f:
    data = json.load(f)

fbx_path              = data.get("FbxPath")
material_names        = data.get("MaterialNames")
diffuse_textures      = data.get("DiffuseTexturePaths")
mask_textures         = data.get("MaskTexturePaths")
normal_textures       = data.get("NormalTexturePaths")
specular_textures     = data.get("SpecularTexturePaths")
codename              = data.get("Codename")
physics_asset_paths   = data.get("PhysicsJsonPaths")
icon_textures         = data.get("IconTexturePaths")
pid                   = data.get("PID")
sound_paths           = data.get("SoundWavPaths")
package_path          = data.get("UePickaxesPackagePath")
use_physics           = len(physics_asset_paths) != 0
mesh_name             = os.path.splitext(os.path.basename(fbx_path))[0]

fbx_destination_path = "{}/{}/Meshes".format(package_path, codename)
tex_destination_path = "{}/{}/Textures".format(package_path, codename)
mi_destination_path  = "{}/{}/Materials".format(package_path, codename)
sounds_destination_path = "{}/{}/Sounds".format(package_path, codename)
EXISTING_SKELETON_PATH = None
unreal.EditorAssetLibrary.load_asset("/Game/CID_Template")


def delete_directory_if_exists(path):
    if unreal.EditorAssetLibrary.does_directory_exist(path):
        unreal.EditorAssetLibrary.delete_directory(path)
        unreal.log("Deleted existing directory: {}".format(path))


delete_directory_if_exists(fbx_destination_path)
delete_directory_if_exists(tex_destination_path)
delete_directory_if_exists(mi_destination_path)
delete_directory_if_exists(sounds_destination_path)


def import_fbx(fbx_path, asset_name, use_custom_skeleton=False):
    skel_data = unreal.FbxSkeletalMeshImportData()
    skel_data.set_editor_property("import_content_type", unreal.FBXImportContentType.FBXICT_ALL)
    skel_data.set_editor_property("import_translation",  unreal.Vector(0.0, 0.0, 0.0))
    skel_data.set_editor_property("import_rotation",     unreal.Rotator(0.0, 0.0, 0.0))
    skel_data.set_editor_property("import_uniform_scale", 1.0)
    skel_data.set_editor_property("convert_scene",       True)
    skel_data.set_editor_property("force_front_x_axis",  False)
    skel_data.set_editor_property("convert_scene_unit",  False)
    skel_data.set_editor_property("import_morph_targets", False)

    ui = unreal.FbxImportUI()
    ui.import_as_skeletal   = True
    ui.import_mesh          = True
    ui.import_animations    = False
    ui.import_materials     = False
    ui.import_textures      = True
    ui.create_physics_asset = False
    ui.mesh_type_to_import  = unreal.FBXImportType.FBXIT_SKELETAL_MESH
    ui.skeletal_mesh_import_data = skel_data

    if not use_custom_skeleton:
        target_skeleton_path = "/Game/Weapons/FORT_Melee/Meshes/SK_Melee_Skeleton"
        sk = unreal.load_asset(target_skeleton_path)
        if sk:
            ui.skeleton = sk
        else:
            unreal.log_error("Failed to load base head skeleton at: {}".format(target_skeleton_path))

    task                  = unreal.AssetImportTask()
    task.filename         = fbx_path
    task.destination_path = fbx_destination_path
    task.destination_name = asset_name
    task.replace_existing = True
    task.automated        = True
    task.save             = True
    task.options          = ui

    unreal.AssetToolsHelpers.get_asset_tools().import_asset_tasks([task])

    if task.imported_object_paths:
        for path in task.imported_object_paths:
            unreal.log("SUCCESS => {}".format(path))
    else:
        unreal.log_error("FAILED — no assets imported.")


def create_material_instance(mi_name):
    factory = unreal.MaterialInstanceConstantFactoryNew()

    asset_tools = unreal.AssetToolsHelpers.get_asset_tools()
    material_instance = asset_tools.create_asset(
        mi_name,
        mi_destination_path,
        unreal.MaterialInstanceConstant,
        factory
    )

    if material_instance:
        unreal.EditorAssetLibrary.save_loaded_asset(material_instance)
        unreal.log("SUCCESS => {}/{}".format(mi_destination_path, mi_name))
    else:
        unreal.log_error("FAILED — could not create material instance: {}".format(mi_name))

    return material_instance


def apply_materials_to_mesh(asset_name):
    mesh_path = "{}/{}".format(fbx_destination_path, asset_name)
    mesh = unreal.load_asset(mesh_path)

    if not mesh:
        unreal.log_error("FAILED — could not load mesh: {}".format(mesh_path))
        return

    materials = mesh.materials
    for i, skeletal_material in enumerate(materials):
        slot_name = str(skeletal_material.material_slot_name)
        target_name = "M_None" if slot_name.lower() == "none" else slot_name
        mi_path = "{}/{}".format(mi_destination_path, target_name)
        mi        = unreal.load_asset(mi_path)
        if mi:
            materials[i] = unreal.SkeletalMaterial(
                material_interface=mi,
                material_slot_name=skeletal_material.material_slot_name
            )
            unreal.log("Applied {} => {}".format(slot_name, asset_name))
        else:
            unreal.log_error("Material not found: {}".format(mi_path))

    mesh.set_editor_property("materials", materials)
    unreal.EditorAssetLibrary.save_loaded_asset(mesh)


def create_anim_blueprint(anim_bp_name, skeleton_asset_name):
    skeleton_path = "{}/{}".format(fbx_destination_path, skeleton_asset_name)
    skeleton = unreal.load_asset(skeleton_path)

    if not skeleton:
        unreal.log_error("FAILED — could not load skeleton: {}".format(skeleton_path))
        return None

    anim_bp = unreal.PhysicsImporter.create_anim_blueprint(
        fbx_destination_path,
        anim_bp_name,
        skeleton
    )

    if not anim_bp:
        unreal.log_error("FAILED — could not create anim blueprint: {}".format(anim_bp_name))
        return None

    unreal.log("SUCCESS => {}/{}".format(fbx_destination_path, anim_bp_name))
    return anim_bp


def import_texture(texture_path, texture_type):
    task                  = unreal.AssetImportTask()
    task.filename         = texture_path
    task.destination_path = tex_destination_path
    task.destination_name = os.path.splitext(os.path.basename(texture_path))[0]
    task.replace_existing = True
    task.automated        = True
    task.save             = False

    unreal.AssetToolsHelpers.get_asset_tools().import_asset_tasks([task])

    if task.imported_object_paths:
        asset_path = task.imported_object_paths[0]
        texture    = unreal.load_asset(asset_path)
        if texture_type == "diffuse":
            texture.lod_group = unreal.TextureGroup.TEXTUREGROUP_CHARACTER
            texture.compression_settings = unreal.TextureCompressionSettings.TC_DEFAULT
        elif texture_type == "specular":
            texture.compression_settings = unreal.TextureCompressionSettings.TC_MASKS
            texture.lod_group            = unreal.TextureGroup.TEXTUREGROUP_CHARACTER_SPECULAR
            texture.srgb                 = False
        elif texture_type == "normal":
            texture.lod_group = unreal.TextureGroup.TEXTUREGROUP_CHARACTER_NORMAL_MAP
        elif texture_type == "icon":
            texture.lod_group = unreal.TextureGroup.TEXTUREGROUP_UI
        unreal.EditorAssetLibrary.save_loaded_asset(texture)
        unreal.log("SUCCESS => {}".format(asset_path))
    else:
        unreal.log_error("FAILED => {}".format(texture_path))


def create_fake_pid():
    template_path = "/Game/CID_Template"
    new_path      = "{}/{}/{}".format(package_path, codename, pid)

    if unreal.EditorAssetLibrary.does_asset_exist(new_path):
        unreal.EditorAssetLibrary.delete_asset(new_path)

    success = unreal.EditorAssetLibrary.duplicate_asset(template_path, new_path)

    if success:
        da = unreal.load_asset(new_path)
        unreal.EditorAssetLibrary.save_loaded_asset(da)
        unreal.log("SUCCESS => {}".format(new_path))
    else:
        unreal.log_error("FAILED — could not create fake pid: {}".format(pid))


def run_physics_importer(mesh_asset_name, json_path):
    physics_dest_path = "{}/{}/Meshes".format(package_path, codename)
    physics_asset_name = "{}".format(os.path.splitext(os.path.basename(json_path))[0])
    skeletal_mesh_path = "{}/{}".format(physics_dest_path, mesh_asset_name)

    physics_json_path = json_path

    with open(physics_json_path, 'r') as f:
        json_content_string = f.read()

    physics_asset = None
    try:
        physics_asset = unreal.PhysicsImporter.import_physics_asset(
            json_content_string,
            physics_dest_path,
            physics_asset_name,
            skeletal_mesh_path
        )

        if physics_asset:
            unreal.log("SUCCESS => Created Physics Asset: {}/{}".format(physics_dest_path, physics_asset_name))
            unreal.log("SUCCESS => Linked to Skeletal Mesh: {}".format(skeletal_mesh_path))
        else:
            unreal.log_error("FAILED — Importer returned null. Check if JSON data matches internal structural exports.")


    except Exception as e:
        unreal.log_error("FAILED — Execution error trying to call PhysicsImporter: {}".format(str(e)))

    return physics_asset

def apply_lod_settings(asset_name):
    mesh_path = "{}/{}".format(fbx_destination_path, asset_name)
    lod_settings_path = "/Game/Characters/Player/Common/LODSettings/Pickaxe_LODSettings"

    mesh = unreal.load_asset(mesh_path)
    lod_settings_asset = unreal.load_asset(lod_settings_path)

    if mesh and lod_settings_asset:
        mesh.set_editor_property("lod_settings", lod_settings_asset)
        unreal.EditorSkeletalMeshLibrary.regenerate_lod(
            skeletal_mesh=mesh,
            new_lod_count=4,
            regenerate_even_if_imported=True,
            generate_base_lod=False
        )
        unreal.log("SUCCESS => Applied LOD Settings & 4 LODs to {}".format(asset_name))
    else:
        unreal.log_error("FAILED => Missing mesh ({}) or LODSettings asset ({})".format(mesh_path, lod_settings_path))


def import_sound(wav_path, sound_name):
    if not wav_path or not os.path.exists(wav_path):
        return

    task = unreal.AssetImportTask()
    task.filename = wav_path
    task.destination_path = sounds_destination_path
    task.destination_name = sound_name
    task.replace_existing = True
    task.automated = True
    task.save = False

    unreal.AssetToolsHelpers.get_asset_tools().import_asset_tasks([task])

    if task.imported_object_paths:
        sound_wave = unreal.load_asset(task.imported_object_paths[0])
        if sound_wave:
            sound_wave.set_editor_property("streaming", True)
            unreal.EditorAssetLibrary.save_loaded_asset(sound_wave)
            unreal.log("SUCCESS => Imported sound: {}".format(task.imported_object_paths[0]))
    else:
        unreal.log_error("FAILED => Could not import sound: {}".format(wav_path))






for name in material_names:
    safe_name = "M_None" if name.lower() == "none" else name
    create_material_instance(safe_name)

physics_assets = []
for physics_asset_path in physics_asset_paths:
    physics_asset = run_physics_importer(mesh_name, physics_asset_path)
    physics_assets.append(physics_asset)

import_fbx(fbx_path, mesh_name, use_physics)
mesh = unreal.EditorAssetLibrary.load_asset("{}/{}".format(fbx_destination_path, mesh_name))

if use_physics:
    anim_bp = create_anim_blueprint("{}_AnimBP".format(mesh_name), "{}_Skeleton".format(mesh_name))
    unreal.PhysicsImporter.build_anim_graph(anim_bp, physics_assets)
    unreal.EditorAssetLibrary.save_loaded_asset(anim_bp)
    mesh.set_editor_property(
    "post_process_anim_blueprint",
    unreal.load_class(None, "{0}/{1}_AnimBP.{1}_AnimBP_C".format(fbx_destination_path, mesh_name))
)

apply_materials_to_mesh(mesh_name)
apply_lod_settings(mesh_name)
unreal.EditorAssetLibrary.save_loaded_asset(mesh)

for i in range(len(diffuse_textures)):
    import_texture(diffuse_textures[i], "diffuse")

for i in range(len(mask_textures)):
    import_texture(mask_textures[i], "specular")

for i in range(len(normal_textures)):
    import_texture(normal_textures[i], "normal")

for i in range(len(specular_textures)):
    import_texture(specular_textures[i], "specular")

for i in range(len(icon_textures)):
    if icon_textures[i] != "":
        import_texture(icon_textures[i], "icon")

for i in range(len(sound_paths)):
    import_sound(sound_paths[i], os.path.splitext(os.path.basename(sound_paths[i]))[0])

create_fake_pid()

unreal.EditorLoadingAndSavingUtils.save_dirty_packages(False, True)