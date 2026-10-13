"""
The four playable characters, modelled to match the reference cards:
  0 Tycoon  - navy top hat with red band, red frock coat, white shirt, navy trousers
  1 Skater  - backwards green cap, yellow hoodie with drawstrings, navy jeans
  2 Student - backwards blue cap, navy jacket with red shirt/sleeves, purple backpack, grey trousers
  3 Showman - black fedora, black hair, red jacket with black zigzag trim, sparkly white glove, white socks

Each figure is ~1 unit tall, faces +Z in the game, and is built as rigid parts under pivot nodes
named LegL / LegR / Body / ArmL / ArmR / Head (origins at the joints) so the game can animate them.

Run inside Blender (via the Blender MCP):
    import sys; sys.path.insert(0, "C:/Monopoly/blender")
    import importlib, mono_lib, characters; importlib.reload(mono_lib); importlib.reload(characters)
    result = characters.build_all()
"""

import mono_lib as L

NAMES = ["Tycoon", "Skater", "Student", "Showman"]
MODEL_DIR = L.GAME_ASSETS + "/models/characters"
PORTRAIT_DIR = L.GAME_ASSETS + "/portraits"
PREVIEW_DIR = L.PROJECT + "/blender/previews"

SKIN = "#f0b48e"
SKIN_SHADE = "#e3a37e"
EYE = "#3a2620"
BROWN_HAIR = "#6b4026"
BROWN_SHOE = "#5a3524"
WHITE = "#eef1f4"

OUTFITS = {
    0: dict(top="#c4333a", sleeve="#c4333a", pants="#2e3a66", shoes=BROWN_SHOE, hair=BROWN_HAIR),
    1: dict(top="#eab43a", sleeve="#eab43a", pants="#2f3d6e", shoes=BROWN_SHOE, hair=BROWN_HAIR),
    2: dict(top="#2d3f7a", sleeve="#c3343a", pants="#3d3f48", shoes=BROWN_SHOE, hair=BROWN_HAIR),
    3: dict(top="#c62f2f", sleeve="#c62f2f", pants="#1d1d22", shoes="#15151a", hair="#18181c"),
}


def build(character_id):
    name = NAMES[character_id]
    scene, col = L.studio_scene(name)
    o = OUTFITS[character_id]
    m = lambda key, color, **kw: L.material(f"MP_{name}_{key}", color, **kw)
    skin, skin_shade, eye = m("Skin", SKIN), m("SkinShade", SKIN_SHADE), m("Eye", EYE, roughness=0.4)
    top, sleeve, pants, shoes, hair = m("Top", o["top"]), m("Sleeve", o["sleeve"]), m("Pants", o["pants"]), m("Shoes", o["shoes"]), m("Hair", o["hair"])

    root = L.empty(name, col)

    # --- legs (pivot at the hip)
    for side, x in (("L", -0.085), ("R", 0.085)):
        leg = L.empty("Leg" + side, col, root, (x, 0.34, 0))
        L.box(f"{name}_Leg{side}", col, (0.14, 0.30, 0.16), (0, -0.15, 0), pants, leg)
        if character_id == 3:
            L.box(f"{name}_Sock{side}", col, (0.146, 0.05, 0.166), (0, -0.265, 0), m("Sock", WHITE), leg, bevel=0.004)
        L.box(f"{name}_Shoe{side}", col, (0.15, 0.07, 0.22), (0, -0.305, 0.03), shoes, leg)

    # --- torso
    body = L.empty("Body", col, root, (0, 0.34, 0))
    L.box(f"{name}_Torso", col, (0.38, 0.32, 0.22), (0, 0.16, 0), top, body)
    if character_id == 0:
        L.box(f"{name}_Shirt", col, (0.1, 0.17, 0.012), (0, 0.22, 0.112), m("Shirt", WHITE), body, bevel=0.003)
        for x in (-0.07, 0.07):
            L.box(f"{name}_Lapel{x:+}", col, (0.045, 0.18, 0.014), (x, 0.2, 0.113), m("Lapel", "#a3262d"), body, bevel=0.003)
        for y in (0.11, 0.05):
            L.box(f"{name}_Button{y}", col, (0.032, 0.032, 0.014), (0.045, y, 0.114), m("Button", "#2b2f4a"), body, bevel=0.004)
        L.box(f"{name}_CoatSkirt", col, (0.385, 0.15, 0.2), (0, -0.07, -0.012), top, body)
        L.box(f"{name}_CoatTail", col, (0.34, 0.12, 0.05), (0, -0.19, -0.085), top, body)
    elif character_id == 1:
        L.box(f"{name}_Hood", col, (0.3, 0.1, 0.1), (0, 0.31, -0.13), m("Hood", "#d39f2c"), body)
        for x in (-0.05, 0.05):
            L.box(f"{name}_String{x:+}", col, (0.022, 0.13, 0.014), (x, 0.17, 0.113), m("String", "#d9d9d9"), body, bevel=0.003)
        L.box(f"{name}_Pocket", col, (0.24, 0.07, 0.014), (0, 0.05, 0.113), m("Pocket", "#d8a330"), body, bevel=0.003)
    elif character_id == 2:
        L.box(f"{name}_Shirt", col, (0.12, 0.3, 0.012), (0, 0.16, 0.112), m("Shirt", "#c3343a"), body, bevel=0.003)
        L.box(f"{name}_Hem", col, (0.385, 0.03, 0.225), (0, 0.015, 0), m("Shirt", "#c3343a"), body, bevel=0.004)
        L.box(f"{name}_Logo", col, (0.045, 0.045, 0.012), (-0.12, 0.09, 0.113), m("Shirt", "#c3343a"), body, bevel=0.003)
        bag = m("Backpack", "#7b3fc4")
        L.box(f"{name}_Backpack", col, (0.3, 0.3, 0.12), (0, 0.17, -0.17), bag, body)
        L.box(f"{name}_BagPocket", col, (0.2, 0.12, 0.04), (0, 0.09, -0.24), m("BagPocket", "#6a33ad"), body)
        for x in (-0.12, 0.12):
            L.box(f"{name}_Strap{x:+}", col, (0.05, 0.24, 0.016), (x, 0.2, 0.113), bag, body, bevel=0.004)
    else:
        L.box(f"{name}_Shirt", col, (0.12, 0.3, 0.012), (0, 0.16, 0.112), m("Shirt", WHITE), body, bevel=0.003)
        trim = m("Trim", "#141418")
        for side in (-1, 1):
            for i, y in enumerate((0.27, 0.2, 0.13, 0.06)):
                x = side * (0.085 + (0.025 if i % 2 else 0))
                L.box(f"{name}_Zig{side}{i}", col, (0.03, 0.075, 0.014), (x, y, 0.113), trim, body, bevel=0.003)
        L.box(f"{name}_Belt", col, (0.385, 0.04, 0.225), (0, 0.02, 0), trim, body, bevel=0.004)

    # --- arms (pivot at the shoulder)
    for side, x in (("L", -0.25), ("R", 0.25)):
        arm = L.empty("Arm" + side, col, root, (x, 0.62, 0))
        L.box(f"{name}_Sleeve{side}", col, (0.11, 0.26, 0.13), (0, -0.12, 0), sleeve, arm)
        if character_id == 3:
            L.box(f"{name}_Stripe{side}", col, (0.116, 0.03, 0.136), (0, -0.04, 0), m("Trim", "#141418"), arm, bevel=0.003)
        if character_id == 3 and side == "R":
            glove = L.material(f"MP_{name}_Glove", "#f6f6ff", roughness=0.18, metallic=0.75, emission=0.35)
            L.box(f"{name}_Glove", col, (0.13, 0.11, 0.14), (0, -0.29, 0), glove, arm, bevel=0.01)
        else:
            L.box(f"{name}_Hand{side}", col, (0.11, 0.09, 0.12), (0, -0.29, 0), skin, arm)

    # --- head (pivot at the neck)
    head = L.empty("Head", col, root, (0, 0.66, 0))
    L.box(f"{name}_Head", col, (0.36, 0.32, 0.34), (0, 0.16, 0), skin, head, bevel=0.016)
    for x in (-0.08, 0.08):
        L.box(f"{name}_Eye{x:+}", col, (0.05, 0.075, 0.012), (x, 0.16, 0.172), eye, head, bevel=0.003)
    L.box(f"{name}_Nose", col, (0.045, 0.055, 0.05), (0, 0.095, 0.19), skin_shade, head, bevel=0.006)
    for x in (-0.19, 0.19):
        L.box(f"{name}_Ear{x:+}", col, (0.03, 0.08, 0.06), (x, 0.14, 0.0), skin_shade, head, bevel=0.005)
    L.box(f"{name}_HairTop", col, (0.372, 0.07, 0.352), (0, 0.295, -0.004), hair, head)
    L.box(f"{name}_HairBack", col, (0.372, 0.24, 0.07), (0, 0.18, -0.145), hair, head)
    for x in (-0.18, 0.18):
        L.box(f"{name}_Sideburn{x:+}", col, (0.022, 0.13, 0.09), (x * 1.02, 0.22, -0.06), hair, head, bevel=0.004)

    hat = L.empty("Hat", col, head, (0, 0, 0))
    if character_id == 0:
        navy = m("Hat", "#383f62")
        L.box(f"{name}_Brim", col, (0.47, 0.04, 0.45), (0, 0.335, 0), navy, hat)
        L.box(f"{name}_Band", col, (0.315, 0.065, 0.315), (0, 0.39, 0), m("Band", "#b5262c"), hat)
        L.box(f"{name}_Crown", col, (0.305, 0.27, 0.305), (0, 0.555, 0), navy, hat)
    elif character_id in (1, 2):
        cap = m("Cap", "#4f9d5a" if character_id == 1 else "#4c63ad")
        visor = m("Visor", "#3f8549" if character_id == 1 else "#3c5296")
        L.box(f"{name}_CapCrown", col, (0.392, 0.135, 0.372), (0, 0.36, 0), cap, hat, bevel=0.02)
        L.box(f"{name}_CapTop", col, (0.33, 0.04, 0.31), (0, 0.44, 0), cap, hat, bevel=0.012)
        L.box(f"{name}_Visor", col, (0.31, 0.03, 0.17), (0, 0.31, -0.26), visor, hat, bevel=0.008)
        L.box(f"{name}_Strap", col, (0.12, 0.05, 0.012), (0, 0.35, 0.187), m("Strap", BROWN_HAIR), hat, bevel=0.003)
    else:
        black = m("Hat", "#1e1e24")
        L.box(f"{name}_Brim", col, (0.53, 0.035, 0.51), (0, 0.335, 0), black, hat)
        L.box(f"{name}_Band", col, (0.345, 0.05, 0.325), (0, 0.375, 0), m("Band", "#3a3a44"), hat)
        L.box(f"{name}_Crown", col, (0.34, 0.12, 0.32), (0, 0.455, 0), black, hat)
        L.box(f"{name}_Dent", col, (0.09, 0.02, 0.24), (0, 0.52, 0), m("Dent", "#2a2a32"), hat, bevel=0.004)
        L.box(f"{name}_LongHair", col, (0.38, 0.2, 0.08), (0, 0.11, -0.16), hair, head)
        for x in (-0.19, 0.19):
            L.box(f"{name}_Lock{x:+}", col, (0.035, 0.2, 0.13), (x, 0.12, -0.04), hair, head, bevel=0.006)
        L.box(f"{name}_Bangs", col, (0.09, 0.05, 0.02), (-0.1, 0.27, 0.176), hair, head, bevel=0.004)

    return scene, col, root


def build_all(render=True):
    report = {}
    for character_id, name in enumerate(NAMES):
        scene, col, root = build(character_id)
        glb = L.export_glb(col, f"{MODEL_DIR}/{name.lower()}.glb")
        entry = {"glb": glb, "objects": len(col.all_objects)}
        if render:
            L.studio_lights(col)
            portrait = L.render_camera(col, (0, 0.78, 0), distance=4, yaw_deg=28, pitch_deg=10, ortho_scale=0.78, name="PortraitCam")
            entry["engine"] = L.render_png(portrait, f"{PORTRAIT_DIR}/{name.lower()}.png", size=256)
            full = L.render_camera(col, (0, 0.55, 0), distance=4, yaw_deg=32, pitch_deg=22, ortho_scale=1.45, name="FullCam")
            L.render_png(full, f"{PREVIEW_DIR}/{name.lower()}_full.png", size=512)
            entry["portrait"] = f"{PORTRAIT_DIR}/{name.lower()}.png"
        report[name] = entry
    keep = {"MP_" + n for n in NAMES}
    report["blend"] = L.save_sources(L.PROJECT + "/blender/characters.blend", prefix="MP_")
    report["scenes"] = sorted(keep)
    return report
