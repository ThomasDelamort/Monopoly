"""
Board props and map pieces for the hybrid map: Blender models every 3D asset, and the game places
them (tile positions come from the board's circular linked list in code).

Sizes/origins match what game/scripts/Board/*.cs expects (game units, origin at the bottom center,
facing +Z) so each .glb drops in place of the procedural fallback.

Run inside Blender (via the Blender MCP):
    import sys; sys.path.insert(0, "C:/Monopoly/blender")
    import importlib, mono_lib, props; importlib.reload(mono_lib); importlib.reload(props)
    result = props.build_all()
"""

import math

import bpy

import mono_lib as L

PROP_DIR = L.GAME_ASSETS + "/models/props"
PREVIEW_DIR = L.PROJECT + "/blender/previews"


def M(name, color, **kw):
    return L.material("MP_" + name, color, **kw)


# ----------------------------------------------------------------------------- scenery

def pine(col):
    root = L.empty("Pine", col)
    L.cylinder("Trunk", col, 0.05, 0.045, 0.2, (0, 0.1, 0), M("Bark", "#6b4628"), root, segments=6)
    for i, (r, h, y, c) in enumerate(((0.3, 0.42, 0.36, "#2f8f46"), (0.24, 0.36, 0.58, "#37a34f"), (0.16, 0.3, 0.78, "#41b55a"))):
        L.cylinder(f"Tier{i}", col, r, 0.0, h, (0, y, 0), M(f"Leaves{i}", c, roughness=0.9), root, segments=7)
    return root


def cottage(col):
    root = L.empty("Cottage", col)
    L.box("Walls", col, (0.42, 0.3, 0.36), (0, 0.15, 0), M("Plaster", "#f1ede4"), root, bevel=0.01)
    L.prism_roof("Roof", col, 0.5, 0.2, 0.44, (0, 0.3, 0), M("RoofRed", "#d9473f"), root)
    L.box("Chimney", col, (0.06, 0.12, 0.06), (0.12, 0.42, -0.06), M("Brick", "#a5483c"), root, bevel=0.004)
    L.box("Door", col, (0.09, 0.16, 0.02), (0.07, 0.08, 0.181), M("Door", "#7a4a2c"), root, bevel=0.003)
    L.box("Window", col, (0.09, 0.08, 0.02), (-0.1, 0.18, 0.181), M("Glass", "#9fd8f5", roughness=0.2), root, bevel=0.003)
    L.box("Step", col, (0.14, 0.03, 0.06), (0.07, 0.015, 0.21), M("Stone", "#a7a3b0"), root, bevel=0.004)
    return root


def apartment(col):
    root = L.empty("Apartment", col)
    L.box("Tower", col, (0.8, 1.5, 0.8), (0, 0.75, 0), M("BrickRed", "#c0574a"), root, bevel=0.015)
    L.box("Roof", col, (0.86, 0.08, 0.86), (0, 1.52, 0), M("BrickDark", "#8f3a33"), root, bevel=0.01)
    L.box("Door", col, (0.2, 0.26, 0.02), (0, 0.13, 0.405), M("Door", "#7a4a2c"), root, bevel=0.004)
    lit = M("WindowLit", "#ffe59a", roughness=0.3, emission=1.5)
    for floor in range(4):
        for c in range(2):
            y = 0.42 + floor * 0.28
            L.box(f"WinF{floor}{c}", col, (0.16, 0.16, 0.02), (-0.18 + c * 0.36, y, 0.405), lit, root, bevel=0.003)
            L.box(f"WinS{floor}{c}", col, (0.02, 0.16, 0.16), (0.405, y, -0.18 + c * 0.36), lit, root, bevel=0.003)
    return root


def vault(col):
    """The bank's safe in the middle of the board."""
    root = L.empty("Vault", col)
    steel, dark, light = M("Steel", "#8d939f", roughness=0.45, metallic=0.5), M("SteelDark", "#5d636e", metallic=0.4), M("SteelLight", "#c9ced6", metallic=0.7, roughness=0.3)
    L.box("Body", col, (1.4, 1.3, 1.3), (0, 0.65, 0), steel, root, bevel=0.05)
    L.box("Door", col, (1.0, 0.95, 0.05), (0, 0.65, 0.66), M("SteelDoor", "#9aa1ad", metallic=0.5, roughness=0.4), root, bevel=0.02)
    L.cylinder("Dial", col, 0.2, 0.2, 0.08, (-0.15, 0.7, 0.7), dark, root, segments=16, axis="Z")
    L.cylinder("Knob", col, 0.08, 0.08, 0.1, (-0.15, 0.7, 0.76), light, root, segments=12, axis="Z")
    L.box("Handle", col, (0.06, 0.36, 0.06), (0.3, 0.7, 0.72), light, root, bevel=0.01)
    for x in (-0.42, 0.42):
        for y in (0.3, 1.0):
            L.cylinder(f"Hinge{x}{y}", col, 0.04, 0.04, 0.12, (x * 1.1, y, 0.66), dark, root, segments=8)
    L.box("Plinth", col, (1.5, 0.08, 1.4), (0, 0.04, 0), dark, root, bevel=0.02)
    return root


def jail_cage(col):
    root = L.empty("JailCage", col)
    L.box("Floor", col, (0.95, 0.03, 0.95), (0, 0.015, 0), M("JailOrange", "#f39223"), root, bevel=0.006)
    bar = M("Bars", "#3a3d46", metallic=0.6, roughness=0.4)
    for i in range(6):
        t = -0.42 + i * 0.168
        L.cylinder(f"BarF{i}", col, 0.018, 0.018, 0.44, (t, 0.22, 0.45), bar, root, segments=6)
        L.cylinder(f"BarS{i}", col, 0.018, 0.018, 0.44, (0.45, 0.22, t), bar, root, segments=6)
    L.box("RailF", col, (0.95, 0.04, 0.04), (0, 0.43, 0.45), bar, root, bevel=0.005)
    L.box("RailS", col, (0.04, 0.04, 0.95), (0.45, 0.43, 0), bar, root, bevel=0.005)
    return root


def car(col, color="#d63a3a", name="Car"):
    root = L.empty(name, col)
    L.box("Body", col, (0.44, 0.1, 0.22), (0, 0.09, 0), M(name + "Paint", color, roughness=0.35), root, bevel=0.025)
    L.box("Cabin", col, (0.22, 0.09, 0.2), (-0.03, 0.18, 0), M("CarGlass", "#9fd8f5", roughness=0.15), root, bevel=0.02)
    for x in (-0.14, 0.14):
        for z in (-0.11, 0.11):
            L.cylinder(f"Wheel{x}{z}", col, 0.045, 0.045, 0.04, (x, 0.045, z), M("Tyre", "#22232a"), root, segments=10, axis="Z")
    return root


# ----------------------------------------------------------------------------- gameplay pieces

def house(col):
    root = L.empty("House", col)
    L.box("Walls", col, (0.17, 0.14, 0.17), (0, 0.07, 0), M("HouseGreen", "#3fae55", roughness=0.6), root, bevel=0.012)
    L.prism_roof("Roof", col, 0.2, 0.1, 0.2, (0, 0.14, 0), M("HouseRoof", "#24753a"), root)
    return root


def hotel(col):
    root = L.empty("Hotel", col)
    L.box("Walls", col, (0.62, 0.2, 0.22), (0, 0.1, 0), M("HotelRed", "#d23a35", roughness=0.6), root, bevel=0.015)
    L.prism_roof("Roof", col, 0.66, 0.12, 0.26, (0, 0.2, 0), M("HotelRoof", "#8e1f1f"), root)
    for i in range(4):
        L.box(f"Win{i}", col, (0.06, 0.06, 0.01), (-0.21 + i * 0.14, 0.11, 0.111), M("HotelWin", "#ffe59a", emission=1.2), root, bevel=0.002)
    return root


def die(col):
    """Centered rounded die. Faces: 1 +Y, 6 -Y, 2 +X, 5 -X, 3 +Z, 4 -Z (game axes)."""
    size = 0.46
    root = L.empty("Die", col)
    L.box("Cube", col, (size, size, size), (0, 0, 0), M("DieWhite", "#f7f5f0", roughness=0.35), root, bevel=0.07)
    pip = M("Pip", "#23232b", roughness=0.4)
    layouts = {
        1: [(0, 0)],
        2: [(-1, -1), (1, 1)],
        3: [(-1, -1), (0, 0), (1, 1)],
        4: [(-1, -1), (1, -1), (-1, 1), (1, 1)],
        5: [(-1, -1), (1, -1), (0, 0), (-1, 1), (1, 1)],
        6: [(-1, -1), (-1, 0), (-1, 1), (1, -1), (1, 0), (1, 1)],
    }
    faces = {1: ((0, 1, 0), (1, 0, 0), (0, 0, 1)), 6: ((0, -1, 0), (1, 0, 0), (0, 0, 1)),
             2: ((1, 0, 0), (0, 1, 0), (0, 0, 1)), 5: ((-1, 0, 0), (0, 1, 0), (0, 0, 1)),
             3: ((0, 0, 1), (1, 0, 0), (0, 1, 0)), 4: ((0, 0, -1), (1, 0, 0), (0, 1, 0))}
    s = 0.115
    for value, (n, u, v) in faces.items():
        axis = "Y" if n[1] else ("X" if n[0] else "Z")
        for k, (a, b) in enumerate(layouts[value]):
            pos = tuple(n[i] * (size / 2 + 0.002) + (u[i] * a + v[i] * b) * s for i in range(3))
            L.cylinder(f"Pip{value}_{k}", col, 0.042, 0.042, 0.016, pos, pip, root, segments=10, axis=axis)
    return root


def pedestal(col):
    """Grass slab on a ring of stone blocks, like the character cards. Top at y = 0.62."""
    root = L.empty("Pedestal", col)
    L.box("Grass", col, (1.9, 0.22, 1.9), (0, 0.51, 0), M("Grass", "#9cc85a", roughness=0.9), root, bevel=0.03)
    L.box("GrassEdge", col, (1.92, 0.06, 1.92), (0, 0.39, 0), M("GrassDark", "#7da246"), root, bevel=0.01)
    for i in range(4):
        for side in range(4):
            t = -0.72 + i * 0.48
            pos = [(t, 0.18, 0.72), (t, 0.18, -0.72), (0.72, 0.18, t), (-0.72, 0.18, t)][side]
            shade = "#8e8a98" if (i + side) % 2 else "#9d99a7"
            L.box(f"Stone{i}{side}", col, (0.45, 0.36, 0.45), pos, M("Stone" + shade, shade), root, bevel=0.03)
    L.box("Core", col, (1.4, 0.36, 1.4), (0, 0.18, 0), M("StoneCore", "#7c7886"), root, bevel=0.0)
    return root


# ----------------------------------------------------------------------------- tile icons

def text_mesh(col, name, text, size, extrude, mat, parent, location):
    """Extruded 3D text converted to a mesh, lying flat with its top pointing into the board (-Z game)."""
    curve = bpy.data.curves.new(name + "Curve", "FONT")
    curve.body = text
    curve.size = size
    curve.extrude = extrude
    curve.align_x = "CENTER"
    curve.align_y = "CENTER"
    temp = bpy.data.objects.new(name + "Temp", curve)
    col.objects.link(temp)
    depsgraph = bpy.context.evaluated_depsgraph_get()
    mesh = bpy.data.meshes.new_from_object(temp.evaluated_get(depsgraph))
    bpy.data.objects.remove(temp, do_unlink=True)
    bpy.data.curves.remove(curve)
    obj = bpy.data.objects.new(name, mesh)
    col.objects.link(obj)
    obj.parent = parent
    obj.location = L.g2b(location)
    mesh.materials.clear()
    mesh.materials.append(mat)
    return obj


def icon_chance(col):
    root = L.empty("IconChance", col)
    text_mesh(col, "QuestionMark", "?", 0.62, 0.03, M("ChanceOrange", "#f07f1a", roughness=0.5), root, (0, 0.035, 0))
    return root


def icon_railroad(col):
    root = L.empty("IconRailroad", col)
    black, red = M("Loco", "#2a2d36", roughness=0.5), M("LocoRed", "#c43b3b")
    L.cylinder("Boiler", col, 0.07, 0.07, 0.3, (0.04, 0.12, 0), black, root, segments=10, axis="X")
    L.box("Cab", col, (0.14, 0.17, 0.18), (-0.16, 0.14, 0), black, root, bevel=0.01)
    L.box("CabRoof", col, (0.18, 0.025, 0.21), (-0.16, 0.235, 0), red, root, bevel=0.005)
    L.cylinder("Chimney", col, 0.03, 0.045, 0.1, (0.13, 0.23, 0), black, root, segments=8)
    L.box("Chassis", col, (0.44, 0.04, 0.2), (0, 0.03, 0), red, root, bevel=0.006)
    for x in (-0.13, 0.0, 0.13):
        for z in (-0.1, 0.1):
            L.cylinder(f"Wheel{x}{z}", col, 0.04, 0.04, 0.025, (x, 0.04, z), red, root, segments=10, axis="Z")
    return root


def icon_electric(col):
    root = L.empty("IconElectric", col)
    bulb = M("Bulb", "#ffe66b", roughness=0.2, emission=2.0)
    L.cylinder("Glass", col, 0.07, 0.12, 0.12, (0, 0.19, 0), bulb, root, segments=10)
    L.cylinder("Dome", col, 0.12, 0.0, 0.1, (0, 0.3, 0), bulb, root, segments=10)
    L.cylinder("Base", col, 0.06, 0.06, 0.1, (0, 0.08, 0), M("Metal", "#9aa0a8", metallic=0.7, roughness=0.3), root, segments=10)
    return root


def icon_water(col):
    root = L.empty("IconWater", col)
    steel = M("Faucet", "#a8b3bd", metallic=0.8, roughness=0.25)
    L.cylinder("Pipe", col, 0.05, 0.05, 0.22, (0, 0.11, -0.05), steel, root, segments=10)
    L.box("Spout", col, (0.07, 0.07, 0.22), (0, 0.2, 0.05), steel, root, bevel=0.015)
    L.box("Handle", col, (0.2, 0.03, 0.04), (0, 0.26, -0.05), M("Tap", "#3a8fd8"), root, bevel=0.008)
    L.cylinder("Drop", col, 0.04, 0.0, 0.08, (0, 0.1, 0.14), M("Water", "#4fb6ff", roughness=0.1, emission=0.6), root, segments=8)
    return root


def icon_chest(col):
    root = L.empty("IconChest", col)
    L.box("Box", col, (0.36, 0.18, 0.24), (0, 0.09, 0), M("ChestBlue", "#2f6fd0"), root, bevel=0.015)
    L.box("Lid", col, (0.38, 0.08, 0.26), (0, 0.21, 0), M("ChestLid", "#1f4fa0"), root, bevel=0.02)
    gold = M("Gold", "#f1c232", metallic=0.8, roughness=0.3)
    L.box("Lock", col, (0.06, 0.08, 0.02), (0, 0.15, 0.13), gold, root, bevel=0.005)
    for x in (-0.12, 0.12):
        L.box(f"Band{x}", col, (0.03, 0.26, 0.27), (x, 0.13, 0), gold, root, bevel=0.004)
    return root


def icon_income_tax(col):
    root = L.empty("IconIncomeTax", col)
    for i in range(3):
        L.box(f"Bills{i}", col, (0.3, 0.05, 0.16), ((i - 1) * 0.02, 0.03 + i * 0.055, 0), M("Bill", "#6fbf73"), root, bevel=0.006)
    L.box("Strap", col, (0.06, 0.17, 0.17), (0, 0.09, 0), M("Paper", "#f2f2e6"), root, bevel=0.004)
    return root


def icon_luxury_tax(col):
    root = L.empty("IconLuxuryTax", col)
    gold = M("Gold", "#f1c232", metallic=0.8, roughness=0.3)
    segments = 14
    for i in range(segments):
        a = 2 * math.pi * i / segments
        L.box(f"Ring{i}", col, (0.05, 0.035, 0.03), (0.11 * math.cos(a), 0.15 + 0.11 * math.sin(a), 0), gold, root, bevel=0.004,
              rotation=(0, -a, 0))
    L.cylinder("Gem", col, 0.06, 0.0, 0.09, (0, 0.3, 0), M("Gem", "#9ee8ff", roughness=0.05, emission=0.8), root, segments=6)
    return root


def icon_go_arrow(col):
    root = L.empty("IconGoArrow", col)
    red = M("GoRed", "#d6262f", roughness=0.5)
    L.box("Shaft", col, (0.5, 0.03, 0.12), (0.12, 0.02, 0), red, root, bevel=0.006)
    # Triangular slab pointing -X (the direction tokens travel away from GO).
    verts = [L.g2b(v) for v in ((-0.36, 0.005, 0), (-0.13, 0.005, -0.15), (-0.13, 0.005, 0.15),
                                (-0.36, 0.035, 0), (-0.13, 0.035, -0.15), (-0.13, 0.035, 0.15))]
    mesh = bpy.data.meshes.new("ArrowHead")
    mesh.from_pydata(verts, [], [(0, 1, 2), (3, 5, 4), (0, 3, 4, 1), (1, 4, 5, 2), (2, 5, 3, 0)])
    mesh.validate()
    mesh.materials.append(red)
    obj = bpy.data.objects.new("ArrowHead", mesh)
    col.objects.link(obj)
    obj.parent = root
    return root


def icon_jail_cell(col):
    root = jail_cage(col)
    root.name = "IconJailCell"
    return root


def icon_parking(col):
    return car(col, "#d63a3a", "ParkingCar")


def icon_police(col):
    root = L.empty("IconPolice", col)
    L.cylinder("Crown", col, 0.2, 0.2, 0.08, (0, 0.12, 0), M("PoliceBlue", "#1f3f8f"), root, segments=12)
    L.cylinder("Band", col, 0.17, 0.17, 0.09, (0, 0.05, 0), M("PoliceBand", "#17306e"), root, segments=12)
    L.box("Visor", col, (0.22, 0.02, 0.1), (0, 0.02, 0.18), M("Visor", "#101827"), root, bevel=0.005)
    L.box("Badge", col, (0.07, 0.07, 0.02), (0, 0.1, 0.18), M("Gold", "#f1c232", metallic=0.8, roughness=0.3), root, bevel=0.005)
    return root


PROPS = {
    "pine": pine, "cottage": cottage, "apartment": apartment, "vault": vault, "jail_cage": jail_cage, "car": car,
    "house": house, "hotel": hotel, "die": die, "pedestal": pedestal,
    "icon_chance": icon_chance, "icon_railroad": icon_railroad, "icon_electric": icon_electric, "icon_water": icon_water,
    "icon_chest": icon_chest, "icon_income_tax": icon_income_tax, "icon_luxury_tax": icon_luxury_tax,
    "icon_go_arrow": icon_go_arrow, "icon_jail_cell": icon_jail_cell, "icon_parking": icon_parking, "icon_police": icon_police,
}


def build_all(names=None, render=True):
    report = {}
    for key in names or PROPS:
        scene, col = L.studio_scene("Prop_" + key)
        PROPS[key](col)
        path = L.export_glb(col, f"{PROP_DIR}/{key}.glb")
        entry = {"glb": path, "objects": len(col.all_objects)}
        if render:
            L.studio_lights(col)
            cam = L.render_camera(col, (0, 0.25 if key not in ("vault", "apartment", "pedestal") else 0.6, 0), distance=6, yaw_deg=35, pitch_deg=30,
                                  ortho_scale=1.9 if key in ("vault", "apartment", "pedestal") else 0.9, name="PreviewCam")
            L.render_png(cam, f"{PREVIEW_DIR}/{key}.png", size=256)
        report[key] = entry
    report["blend"] = L.save_sources(L.PROJECT + "/blender/props.blend", prefix="MP_Prop_")
    return report
