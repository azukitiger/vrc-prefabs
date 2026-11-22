import os
import sys
import time

import bpy

START = time.time()

FORMATS = {
    ".png": "PNG",
    ".tga": "TARGA",
    ".jpg": "JPEG",
    ".jpeg": "JPEG",
    ".exr": "OPEN_EXR",
    ".bmp": "BMP",
    ".tif": "TIFF",
    ".tiff": "TIFF",
}

USAGE = (
    "Usage: -- <FbxPath> <Material> <FromUV> <ToUV> <Texture> <Output> [Margin] [AUTO|GPU|CPU]\n"
    "   or: -- --list-all <FbxPath>"
)


def log(msg):
    print(f"[{time.time() - START:6.1f}s] {msg}", flush=True)


def fail(msg):
    log("ERROR: " + msg)
    sys.exit(1)


def script_args():
    if "--" not in sys.argv:
        fail("missing '--' before the script arguments")
    return sys.argv[sys.argv.index("--") + 1:]


def import_fbx(path):
    path = os.path.abspath(path)
    if not os.path.isfile(path):
        fail(f"FBX not found: {path}")
    log(f"Importing {path}")
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=path)


def materials_with_objects():
    found = {}
    for obj in bpy.data.objects:
        if obj.type != 'MESH':
            continue
        for slot in obj.material_slots:
            if slot.material and slot.material.name not in found:
                found[slot.material.name] = obj
    return found


def list_all(args):
    if len(args) < 1:
        fail(USAGE)
    import_fbx(args[0])
    found = materials_with_objects()
    print("MATLIST_START", flush=True)
    for name in found:
        print(name, flush=True)
    print("MATLIST_END", flush=True)
    for name, obj in found.items():
        print(f"UVLIST_START:{name}", flush=True)
        for uv in obj.data.uv_layers:
            print(uv.name, flush=True)
        print(f"UVLIST_END:{name}", flush=True)


def is_broken(img):
    try:
        if img.source == 'FILE' and not img.packed_file:
            path = bpy.path.abspath(img.filepath, library=img.library)
            if not path or not os.path.isfile(path):
                return True
        return img.size[0] == 0 or img.size[1] == 0
    except Exception:
        return True


def enable_gpu():
    try:
        prefs = bpy.context.preferences.addons['cycles'].preferences
        for backend in ('OPTIX', 'CUDA', 'HIP', 'ONEAPI', 'METAL'):
            try:
                prefs.compute_device_type = backend
            except TypeError:
                continue
            prefs.get_devices()
            gpus = [d for d in prefs.devices if d.type == backend]
            if gpus:
                for d in prefs.devices:
                    d.use = d.type == backend
                log(f"GPU {backend}: {', '.join(d.name for d in gpus)}")
                return True
        log("No GPU found, using CPU")
    except Exception as e:
        log(f"GPU setup failed ({e}), using CPU")
    return False


def bake(args):
    if len(args) < 6:
        fail(USAGE)
    fbx_path, material_name, source_uv, dest_uv = args[0], args[1], args[2], args[3]
    input_path = os.path.abspath(args[4])
    output_path = os.path.abspath(args[5])
    margin = int(args[6]) if len(args) > 6 else 32
    device = args[7].upper() if len(args) > 7 else "AUTO"

    import_fbx(fbx_path)
    obj = materials_with_objects().get(material_name)
    if obj is None:
        fail(f"no mesh uses the material '{material_name}'")
    mat = bpy.data.materials[material_name]

    uv_layers = obj.data.uv_layers
    if source_uv not in uv_layers or dest_uv not in uv_layers:
        fail(f"'{obj.name}' is missing '{source_uv}' or '{dest_uv}'. UV maps: {[uv.name for uv in uv_layers]}")

    for slot in obj.material_slots:
        other = slot.material
        if not other or other == mat or not other.use_nodes or not other.node_tree:
            continue
        for node in other.node_tree.nodes:
            if node.type == 'TEX_IMAGE' and node.image is not None and is_broken(node.image):
                log(f"Clearing missing image '{node.image.name}' in '{other.name}'")
                node.image = None

    if not os.path.isfile(input_path):
        fail(f"texture not found: {input_path}")
    src_img = bpy.data.images.load(input_path)
    width, height = src_img.size[0], src_img.size[1]
    if width == 0 or height == 0:
        fail(f"couldn't read {input_path}")

    mat.use_nodes = True
    nodes = mat.node_tree.nodes
    links = mat.node_tree.links

    src_node = nodes.new("ShaderNodeTexImage")
    src_node.image = src_img
    uv_node = nodes.new("ShaderNodeUVMap")
    uv_node.uv_map = source_uv
    links.new(uv_node.outputs["UV"], src_node.inputs["Vector"])
    emit_node = nodes.new("ShaderNodeEmission")
    links.new(src_node.outputs["Color"], emit_node.inputs["Color"])

    out_node = next((n for n in nodes if n.type == "OUTPUT_MATERIAL"), None) or nodes.new("ShaderNodeOutputMaterial")
    links.new(emit_node.outputs["Emission"], out_node.inputs["Surface"])

    dest_img = bpy.data.images.new("Bake " + material_name, width, height, alpha=True)
    dest_node = nodes.new("ShaderNodeTexImage")
    dest_node.image = dest_img
    for n in nodes:
        n.select = False
    dest_node.select = True
    nodes.active = dest_node

    uv_layers.active = uv_layers[dest_uv]
    bpy.ops.object.select_all(action='DESELECT')
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj

    scene = bpy.context.scene
    scene.render.engine = 'CYCLES'
    scene.cycles.bake_type = 'EMIT'
    scene.cycles.samples = 1
    scene.cycles.use_denoising = False
    scene.render.bake.margin = margin
    scene.render.bake.margin_type = 'EXTEND'
    scene.render.bake.use_selected_to_active = False

    gpu = device != "CPU" and enable_gpu()
    if device == "GPU" and not gpu:
        log("GPU requested but not available, using CPU")
    scene.cycles.device = 'GPU' if gpu else 'CPU'

    log(f"Baking {source_uv} to {dest_uv} at {width}x{height}, margin {margin}px on {scene.cycles.device}")
    bpy.ops.object.bake(type='EMIT')

    os.makedirs(os.path.dirname(output_path), exist_ok=True)
    dest_img.filepath_raw = output_path
    dest_img.file_format = FORMATS.get(os.path.splitext(output_path)[1].lower(), "PNG")
    dest_img.save()
    log(f"Saved {output_path}")


def main():
    args = script_args()
    if not args:
        fail(USAGE)
    if args[0] == "--list-all":
        list_all(args[1:])
    else:
        bake(args)


if __name__ == "__main__":
    main()
