import bpy
import sys
import os
import time

_START_TIME = time.time()

def log(msg):
    elapsed = time.time() - _START_TIME
    print(f"[{elapsed:6.1f}s] {msg}", flush=True)

EXT_TO_FORMAT = {
    ".png": "PNG",
    ".tga": "TARGA",
    ".jpg": "JPEG",
    ".jpeg": "JPEG",
    ".exr": "OPEN_EXR",
    ".bmp": "BMP",
    ".tif": "TIFF",
    ".tiff": "TIFF",
}

def get_args():
    argv = sys.argv
    if "--" not in argv:
        log("ERROR: expected '--' before script args")
        sys.exit(1)
    idx = argv.index("--")
    return argv[idx + 1:]

def import_fbx_and_find_target(fbx_path, material_name):
    if not os.path.isfile(fbx_path):
        log(f"ERROR: FBX not found: {fbx_path}")
        sys.exit(1)

    log(f"Importing FBX: {fbx_path}")
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=fbx_path)
    log("FBX import complete")

    if material_name not in bpy.data.materials:
        log(f"ERROR: material '{material_name}' not found after FBX import")
        sys.exit(1)
    mat = bpy.data.materials[material_name]

    target_obj = None
    for obj in bpy.data.objects:
        if obj.type == 'MESH':
            if any(s.material and s.material.name == material_name for s in obj.material_slots):
                target_obj = obj
                break
    if not target_obj:
        log(f"ERROR: no mesh object has a material slot named '{material_name}'")
        sys.exit(1)

    return mat, target_obj

def list_all(args):
    if len(args) < 1:
        log("Usage: -- --list-all <FbxPath>")
        sys.exit(1)

    fbx_path = os.path.abspath(args[0])
    if not os.path.isfile(fbx_path):
        log(f"ERROR: FBX not found: {fbx_path}")
        sys.exit(1)

    log(f"Importing FBX: {fbx_path}")
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=fbx_path)
    log("FBX import complete")

    material_names = []
    material_to_obj = {}
    for obj in bpy.data.objects:
        if obj.type == 'MESH':
            for slot in obj.material_slots:
                if slot.material and slot.material.name not in material_to_obj:
                    material_to_obj[slot.material.name] = obj
                    material_names.append(slot.material.name)

    # Machine-readable blocks for Unity's parser - plain, unprefixed, flushed immediately.
    print("MATLIST_START", flush=True)
    for n in material_names:
        print(n, flush=True)
    print("MATLIST_END", flush=True)

    for name in material_names:
        print(f"UVLIST_START:{name}", flush=True)
        obj = material_to_obj[name]
        for uv in obj.data.uv_layers:
            print(uv.name, flush=True)
        print(f"UVLIST_END:{name}", flush=True)

def list_materials(args):
    if len(args) < 1:
        log("Usage: -- --list-materials <FbxPath>")
        sys.exit(1)

    fbx_path = os.path.abspath(args[0])
    if not os.path.isfile(fbx_path):
        log(f"ERROR: FBX not found: {fbx_path}")
        sys.exit(1)

    log(f"Importing FBX: {fbx_path}")
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=fbx_path)
    log("FBX import complete")

    names = []
    for obj in bpy.data.objects:
        if obj.type == 'MESH':
            for slot in obj.material_slots:
                if slot.material and slot.material.name not in names:
                    names.append(slot.material.name)

    # Machine-readable block for Unity's parser - plain, unprefixed, flushed immediately.
    print("MATLIST_START", flush=True)
    for n in names:
        print(n, flush=True)
    print("MATLIST_END", flush=True)

def list_uvs(args):
    if len(args) < 2:
        log("Usage: -- --list-uvs <FbxPath> <MaterialName>")
        sys.exit(1)

    fbx_path = os.path.abspath(args[0])
    material_name = args[1]

    _mat, target_obj = import_fbx_and_find_target(fbx_path, material_name)

    # Machine-readable block for Unity's parser - plain, unprefixed, flushed immediately.
    print("UVLIST_START", flush=True)
    for uv in target_obj.data.uv_layers:
        print(uv.name, flush=True)
    print("UVLIST_END", flush=True)

def is_image_broken(img):
    try:
        if img.source == 'FILE' and not img.packed_file:
            filepath = bpy.path.abspath(img.filepath, library=img.library)
            if not filepath or not os.path.isfile(filepath):
                return True
        return img.size[0] == 0 or img.size[1] == 0
    except Exception:
        return True

def try_enable_gpu(preferred_backend=None):
    try:
        cprefs = bpy.context.preferences.addons['cycles'].preferences
        backends = [preferred_backend] if preferred_backend else ('OPTIX', 'CUDA', 'HIP', 'ONEAPI', 'METAL')

        for backend in backends:
            try:
                cprefs.compute_device_type = backend
            except TypeError:
                continue  # not a valid backend on this platform/build

            cprefs.get_devices()
            gpu_devices = [d for d in cprefs.devices if d.type == backend]
            if gpu_devices:
                for d in cprefs.devices:
                    d.use = (d.type == backend)
                log(f"Enabled GPU backend '{backend}': {[d.name for d in gpu_devices]}")
                return True

        log("No usable GPU backend found for Cycles - using CPU")
        return False
    except Exception as e:
        log(f"Could not configure GPU baking ({e}) - using CPU")
        return False

def bake(args):
    if len(args) < 6:
        log("Usage: -- <FbxPath> <MaterialName> <SourceUVName> <DestUVName> <InputTexturePath> <OutputTexturePath> [Margin] [DeviceMode]")
        sys.exit(1)

    fbx_path = os.path.abspath(args[0])
    material_name = args[1]
    source_uv = args[2]
    dest_uv = args[3]
    input_path = os.path.abspath(args[4])
    output_path = os.path.abspath(args[5])
    margin = int(args[6]) if len(args) > 6 else 32
    device_mode = args[7].upper() if len(args) > 7 else "AUTO"  # AUTO / GPU / CPU

    mat, target_obj = import_fbx_and_find_target(fbx_path, material_name)

    uv_layers = target_obj.data.uv_layers
    if source_uv not in uv_layers or dest_uv not in uv_layers:
        available = [uv.name for uv in uv_layers]
        log(f"ERROR: object '{target_obj.name}' is missing '{source_uv}' or '{dest_uv}' UV map. "
            f"Available: {available}")
        sys.exit(1)

    cleared_count = 0
    for slot in target_obj.material_slots:
        other_mat = slot.material
        if not other_mat or other_mat == mat or not other_mat.use_nodes or not other_mat.node_tree:
            continue
        for node in other_mat.node_tree.nodes:
            if node.type == 'TEX_IMAGE' and node.image is not None and is_image_broken(node.image):
                log(f"  '{other_mat.name}' texture node references a missing/broken image "
                    f"('{node.image.name}') - clearing it so Cycles can't fall back to "
                    f"magenta 'missing texture' color for it during the bake.")
                node.image = None
                cleared_count += 1
    if cleared_count > 0:
        log(f"Cleared {cleared_count} genuinely broken texture reference(s) from other "
            f"material(s) on this mesh so they can't corrupt the bake.")
    else:
        log("No broken textures found on other materials - leaving them untouched.")

    mat.use_nodes = True
    nodes = mat.node_tree.nodes
    links = mat.node_tree.links

    log(f"Loading source texture: {input_path}")
    src_img = bpy.data.images.load(input_path)
    log("Loaded source image")

    # Resolution: match the source image
    src_width, src_height = src_img.size[0], src_img.size[1]
    if src_width == 0 or src_height == 0:
        log("ERROR: could not read source image dimensions")
        sys.exit(1)

    log("Setting up temporary bake nodes...")

    # Temp nodes: source (reads source UV) -> Emission -> Output
    src_node = nodes.new("ShaderNodeTexImage")
    src_node.image = src_img
    src_node.location = (-600, 300)

    uv_node = nodes.new("ShaderNodeUVMap")
    uv_node.uv_map = source_uv
    uv_node.location = (-900, 300)
    links.new(uv_node.outputs["UV"], src_node.inputs["Vector"])

    emit_node = nodes.new("ShaderNodeEmission")
    emit_node.location = (-300, 300)
    links.new(src_node.outputs["Color"], emit_node.inputs["Color"])

    out_node = next((n for n in nodes if n.type == "OUTPUT_MATERIAL"), None)
    if not out_node:
        out_node = nodes.new("ShaderNodeOutputMaterial")

    orig_socket = out_node.inputs["Surface"].links[0].from_socket if out_node.inputs["Surface"].is_linked else None
    links.new(emit_node.outputs["Emission"], out_node.inputs["Surface"])

    # Destination image + node (bake target), same resolution as source
    dest_name = f"BAKE_DEST_{material_name}"
    if dest_name in bpy.data.images:
        bpy.data.images.remove(bpy.data.images[dest_name])
    dest_img = bpy.data.images.new(dest_name, src_width, src_height, alpha=True)

    dest_node = nodes.new("ShaderNodeTexImage")
    dest_node.image = dest_img
    dest_node.location = (0, 600)

    for n in nodes:
        n.select = False
    dest_node.select = True
    nodes.active = dest_node

    # Active UV for baking = destination UV
    uv_layers.active = uv_layers[dest_uv]

    bpy.ops.object.select_all(action='DESELECT')
    target_obj.select_set(True)
    bpy.context.view_layer.objects.active = target_obj

    scene = bpy.context.scene
    scene.render.engine = 'CYCLES'
    scene.cycles.bake_type = 'EMIT'
    scene.render.bake.margin = margin
    scene.render.bake.margin_type = 'EXTEND'
    scene.render.bake.use_selected_to_active = False

    scene.cycles.samples = 1
    scene.cycles.use_denoising = False

    if device_mode == "CPU":
        gpu_ok = False
        log("Device mode: CPU (forced)")
    else:
        gpu_ok = try_enable_gpu()
        if device_mode == "GPU" and not gpu_ok:
            log("WARNING: GPU requested but no backend available - falling back to CPU")
    scene.cycles.device = 'GPU' if gpu_ok else 'CPU'
    log(f"Cycles device: {'GPU' if gpu_ok else 'CPU'}, samples=1")

    log(f"Starting Cycles bake: '{source_uv}' -> '{dest_uv}' at {src_width}x{src_height}, "
        f"margin={margin}px...")
    bpy.ops.object.bake(type='EMIT')
    log("Bake operator finished, writing output file...")

    # Save output, matching source file format where possible
    out_ext = os.path.splitext(output_path)[1].lower()
    file_format = EXT_TO_FORMAT.get(out_ext, "PNG")
    os.makedirs(os.path.dirname(output_path), exist_ok=True)
    dest_img.filepath_raw = output_path
    dest_img.file_format = file_format
    dest_img.save()
    log(f"Saved: {output_path}")

    # Cleanup temp nodes, restore original output link
    for n in (src_node, uv_node, emit_node, dest_node):
        nodes.remove(n)
    if orig_socket:
        links.new(orig_socket, out_node.inputs["Surface"])

def main():
    args = get_args()
    if not args:
        log("Usage: -- <FbxPath> <MaterialName> <SourceUVName> <DestUVName> <InputTexturePath> <OutputTexturePath> [Margin] [DeviceMode]")
        log("   or: -- --list-uvs <FbxPath> <MaterialName>")
        log("   or: -- --list-materials <FbxPath>")
        log("   or: -- --list-all <FbxPath>")
        sys.exit(1)

    if args[0] == "--list-uvs":
        list_uvs(args[1:])
    elif args[0] == "--list-materials":
        list_materials(args[1:])
    elif args[0] == "--list-all":
        list_all(args[1:])
    else:
        bake(args)

if __name__ == "__main__":
    main()