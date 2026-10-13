"""
Shared helpers for the Low-Poly Monopoly asset generators.

These scripts are run inside Blender through the Blender MCP (execute_blender_code):

    import sys; sys.path.insert(0, "C:/Monopoly/blender")
    import importlib, mono_lib, characters
    importlib.reload(mono_lib); importlib.reload(characters)
    result = characters.build_all()

Everything is created inside its own collection so an open user scene is never touched,
then exported as .glb straight into the Godot project (game/assets/...).

Coordinates: the game (Godot / glTF) is Y-up with characters facing +Z. Blender is Z-up and
the glTF exporter maps Blender (x, y, z) -> glTF (x, z, -y), so a model that faces -Y in Blender
faces +Z in the game. The helpers below take sizes/positions in *game* units and axes
(g = (x, y_up, z_forward)) and convert them, so the numbers match the C# fallback models.
"""

import math
import os

import bpy

PROJECT = "C:/Monopoly"
GAME_ASSETS = PROJECT + "/game/assets"

_materials = {}


# ----------------------------------------------------------------------------- coordinates

def g2b(v):
    """Game-space (x, up, forward) -> Blender (x, -forward, up)."""
    x, y, z = v
    return (x, -z, y)


def size_g2b(s):
    sx, sy, sz = s
    return (sx, sz, sy)


# ----------------------------------------------------------------------------- scene housekeeping

def studio_scene(name):
    """
    A dedicated scene "MP_<name>" for one asset (replaced if it exists), so the user's own scenes
    are never modified. Returns (scene, collection) where collection is the scene's root collection.
    """
    scene_name = "MP_" + name
    old = bpy.data.scenes.get(scene_name)
    if old is not None:
        for obj in list(old.collection.all_objects):
            bpy.data.objects.remove(obj, do_unlink=True)
        bpy.data.scenes.remove(old)
    scene = bpy.data.scenes.new(scene_name)
    scene.render.film_transparent = True
    world = bpy.data.worlds.get("MP_World") or bpy.data.worlds.new("MP_World")
    world.color = (0.05, 0.07, 0.12)
    scene.world = world
    return scene, scene.collection


def fresh_collection(name):
    """Backwards-compatible alias: the root collection of a fresh studio scene."""
    return studio_scene(name)[1]


def scene_of(collection):
    for scene in bpy.data.scenes:
        if scene.collection == collection:
            return scene
    return bpy.context.scene


def material(name, color, roughness=0.75, metallic=0.0, emission=0.0):
    """Flat-colored Principled material, cached by name. color = hex string or RGB tuple (sRGB)."""
    if name in _materials and _materials[name].name in bpy.data.materials:
        return _materials[name]
    mat = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    mat.use_nodes = True
    rgb = hex_to_linear(color) if isinstance(color, str) else color
    bsdf = mat.node_tree.nodes.get("Principled BSDF")
    bsdf.inputs["Base Color"].default_value = (*rgb, 1.0)
    bsdf.inputs["Roughness"].default_value = roughness
    bsdf.inputs["Metallic"].default_value = metallic
    if emission > 0:
        bsdf.inputs["Emission Color"].default_value = (*rgb, 1.0)
        bsdf.inputs["Emission Strength"].default_value = emission
    mat.diffuse_color = (*rgb, 1.0)
    _materials[name] = mat
    return mat


def hex_to_linear(hex_color):
    h = hex_color.lstrip("#")
    srgb = [int(h[i:i + 2], 16) / 255.0 for i in (0, 2, 4)]
    return tuple(c / 12.92 if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4 for c in srgb)


# ----------------------------------------------------------------------------- primitives

def empty(name, collection, parent=None, location=(0, 0, 0)):
    """A pivot node (exported to glTF as a plain node). location in game space, relative to parent."""
    obj = bpy.data.objects.new(name, None)
    obj.empty_display_type = "PLAIN_AXES"
    obj.empty_display_size = 0.05
    collection.objects.link(obj)
    obj.parent = parent
    obj.location = g2b(location)
    return obj


def box(name, collection, size, location, mat, parent=None, bevel=0.012, rotation=(0, 0, 0)):
    """
    Axis-aligned cuboid with softly bevelled edges (the 'voxel toy' look of the concept art).
    size/location in game space; location is relative to the parent pivot.
    """
    sx, sy, sz = size_g2b(size)
    hx, hy, hz = sx / 2, sy / 2, sz / 2
    verts = [(-hx, -hy, -hz), (hx, -hy, -hz), (hx, hy, -hz), (-hx, hy, -hz),
             (-hx, -hy, hz), (hx, -hy, hz), (hx, hy, hz), (-hx, hy, hz)]
    faces = [(0, 3, 2, 1), (4, 5, 6, 7), (0, 1, 5, 4), (1, 2, 6, 5), (2, 3, 7, 6), (3, 0, 4, 7)]
    mesh = bpy.data.meshes.new(name)
    mesh.from_pydata(verts, [], faces)
    mesh.update()
    obj = bpy.data.objects.new(name, mesh)
    collection.objects.link(obj)
    obj.parent = parent
    obj.location = g2b(location)
    obj.rotation_euler = rotation
    mesh.materials.append(mat)
    if bevel > 0:
        width = min(bevel, sx * 0.3, sy * 0.3, sz * 0.3)
        mod = obj.modifiers.new("Bevel", "BEVEL")
        mod.width = width
        mod.segments = 2
        mod.limit_method = "NONE"
    return obj


def cylinder(name, collection, radius_bottom, radius_top, height, location, mat, parent=None, segments=8, axis="Y"):
    """Low-poly cylinder/cone along the game's up axis (or along X/Z)."""
    verts, faces = [], []
    for ring, (r, y) in enumerate(((radius_bottom, -height / 2), (radius_top, height / 2))):
        for i in range(segments):
            a = 2 * math.pi * i / segments
            verts.append((r * math.cos(a), y, r * math.sin(a)))
    # Counter-clockwise seen from outside (vertex angle increases toward +Z when viewed from +Y).
    faces.append(tuple(range(segments)))
    if radius_top > 0:
        faces.append(tuple(range(2 * segments - 1, segments - 1, -1)))
    for i in range(segments):
        j = (i + 1) % segments
        faces.append((i, segments + i, segments + j, j))
    # game -> blender for each vertex, optionally re-orienting the axis
    def orient(v):
        x, y, z = v
        if axis == "X":
            x, y = y, -x
        elif axis == "Z":
            y, z = -z, y
        return g2b((x, y, z))
    mesh = bpy.data.meshes.new(name)
    mesh.from_pydata([orient(v) for v in verts], [], faces)
    mesh.update()
    mesh.validate()
    obj = bpy.data.objects.new(name, mesh)
    collection.objects.link(obj)
    obj.parent = parent
    obj.location = g2b(location)
    mesh.materials.append(mat)
    return obj


def prism_roof(name, collection, width, height, depth, location, mat, parent=None):
    """Gable roof: triangular prism, ridge along the game X axis."""
    w, h, d = width / 2, height, depth / 2
    g_verts = [(-w, 0, -d), (w, 0, -d), (w, 0, d), (-w, 0, d), (-w, h, 0), (w, h, 0)]
    faces = [(0, 1, 2, 3), (0, 4, 5, 1), (3, 2, 5, 4), (0, 3, 4), (1, 5, 2)]
    mesh = bpy.data.meshes.new(name)
    mesh.from_pydata([g2b(v) for v in g_verts], [], faces)
    mesh.update()
    mesh.validate()
    obj = bpy.data.objects.new(name, mesh)
    collection.objects.link(obj)
    obj.parent = parent
    obj.location = g2b(location)
    mesh.materials.append(mat)
    return obj


# ----------------------------------------------------------------------------- export / render

def fix_normals(scene):
    """Safety net: make every mesh's faces point outward before export."""
    import bmesh
    for obj in scene.objects:
        if obj.type != "MESH" or obj.data.users > 1:
            continue
        bm = bmesh.new()
        bm.from_mesh(obj.data)
        bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
        bm.to_mesh(obj.data)
        bm.free()
        obj.data.update()


def export_glb(collection, path, animations=False):
    """Exports the objects of one studio scene (modifiers applied) as a binary glTF."""
    os.makedirs(os.path.dirname(path), exist_ok=True)
    scene = scene_of(collection)
    fix_normals(scene)
    view_layer = scene.view_layers[0]
    for obj in scene.objects:
        obj.select_set(obj.type in {"MESH", "EMPTY"}, view_layer=view_layer)
    kwargs = dict(
        filepath=path,
        export_format="GLB",
        use_selection=True,
        export_apply=True,
        export_yup=True,
        export_animations=animations,
        export_cameras=False,
        export_lights=False,
    )
    if animations:
        kwargs["export_animation_mode"] = "ACTIONS"
    with bpy.context.temp_override(scene=scene, view_layer=view_layer):
        bpy.ops.export_scene.gltf(**kwargs)
    return path


def save_sources(path, prefix="MP_"):
    """Writes only our MP_* scenes (and what they use) to a .blend, leaving the open file alone."""
    blocks = {scene for scene in bpy.data.scenes if scene.name.startswith(prefix)}
    os.makedirs(os.path.dirname(path), exist_ok=True)
    bpy.data.libraries.write(path, blocks, fake_user=True)
    return path


def eevee_engine(scene):
    for engine in ("BLENDER_EEVEE", "BLENDER_EEVEE_NEXT"):
        try:
            scene.render.engine = engine
            return engine
        except TypeError:
            continue
    scene.render.engine = "BLENDER_WORKBENCH"
    return "BLENDER_WORKBENCH"


def render_camera(collection, target_game, distance=4.0, yaw_deg=25.0, pitch_deg=12.0, ortho_scale=1.0, name="PortraitCam"):
    """Orthographic camera looking at a game-space target from the front-right (3/4 view)."""
    cam_data = bpy.data.cameras.new(name)
    cam_data.type = "ORTHO"
    cam_data.ortho_scale = ortho_scale
    cam = bpy.data.objects.new(name, cam_data)
    collection.objects.link(cam)
    yaw, pitch = math.radians(yaw_deg), math.radians(pitch_deg)
    tx, ty, tz = target_game
    pos_game = (tx + distance * math.sin(yaw) * math.cos(pitch), ty + distance * math.sin(pitch), tz + distance * math.cos(yaw) * math.cos(pitch))
    cam.location = g2b(pos_game)
    direction = [t - p for t, p in zip(g2b(target_game), cam.location)]
    from mathutils import Vector
    cam.rotation_euler = Vector(direction).to_track_quat("-Z", "Y").to_euler()
    return cam


def studio_lights(collection):
    sun_data = bpy.data.lights.new("KeySun", "SUN")
    sun_data.energy = 3.2
    sun = bpy.data.objects.new("KeySun", sun_data)
    collection.objects.link(sun)
    sun.rotation_euler = (math.radians(50), math.radians(-15), math.radians(-35))
    fill_data = bpy.data.lights.new("FillSun", "SUN")
    fill_data.energy = 1.1
    fill_data.color = (0.75, 0.85, 1.0)
    fill = bpy.data.objects.new("FillSun", fill_data)
    collection.objects.link(fill)
    fill.rotation_euler = (math.radians(70), math.radians(10), math.radians(140))
    return sun, fill


def render_png(camera, path, size=256, transparent=True):
    scene = scene_of(camera.users_collection[0])
    engine = eevee_engine(scene)
    scene.camera = camera
    scene.render.resolution_x = size
    scene.render.resolution_y = size
    scene.render.resolution_percentage = 100
    scene.render.film_transparent = transparent
    scene.render.image_settings.file_format = "PNG"
    scene.render.image_settings.color_mode = "RGBA"
    scene.render.filepath = path
    os.makedirs(os.path.dirname(path), exist_ok=True)
    bpy.ops.render.render(write_still=True, scene=scene.name)
    return engine
