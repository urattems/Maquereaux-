#!/usr/bin/env python3
"""Génère src/Maquereaux.App/Themes/Icons.xaml à partir des icônes Fluent UI System Icons (MIT).
Usage : python3 tools/gen_icons.py <dossier icons/ du paquet @fluentui/svg-icons>
Les chemins SVG sont renormalisés (drapeaux d'arc séparés) car le mini-langage WPF ne lit pas la forme compacte."""
import re, sys, os

ICONS = {
    "Play": "play_20_filled", "Stop": "stop_20_filled", "Pause": "pause_20_filled", "Record": "record_20_filled",
    "Keyboard": "keyboard_20_regular", "Profiles": "layer_20_regular", "Lab": "beaker_20_regular",
    "Settings": "settings_20_regular", "Add": "add_20_regular", "Delete": "delete_20_regular", "Copy": "copy_20_regular",
    "Export": "arrow_export_ltr_20_regular", "Import": "arrow_import_20_regular", "Search": "search_20_regular",
    "Close": "dismiss_20_regular", "Minimize": "subtract_20_regular", "Maximize": "maximize_20_regular",
    "Restore": "square_multiple_20_regular", "ChevronLeft": "chevron_left_20_regular", "ChevronRight": "chevron_right_20_regular",
    "ChevronDown": "chevron_down_20_regular", "Check": "checkmark_20_regular", "Warning": "warning_20_regular",
    "Info": "info_20_regular", "Error": "error_circle_20_regular", "Pin": "pin_20_regular", "Compact": "picture_in_picture_20_regular",
    "Sun": "weather_sunny_20_regular", "Moon": "weather_moon_20_regular", "Refresh": "arrow_sync_20_regular",
    "Timer": "timer_20_regular", "Flash": "flash_20_regular", "Folder": "folder_open_20_regular", "Shield": "shield_20_regular",
    "Menu": "navigation_20_regular", "Grip": "re_order_dots_vertical_20_regular", "Undo": "arrow_undo_20_regular",
    "Window": "window_20_regular", "Histogram": "data_histogram_20_regular", "LineChart": "data_line_20_regular",
    "ZoomIn": "zoom_in_20_regular", "ZoomOut": "zoom_out_20_regular", "Lightbulb": "lightbulb_20_regular",
    "Save": "save_20_regular", "Edit": "edit_20_regular", "Person": "person_20_regular", "Lock": "lock_closed_20_regular",
    "Sparkle": "sparkle_20_regular", "Target": "target_20_regular", "Reset": "arrow_reset_20_regular", "Eye": "eye_20_regular",
    "Open": "open_20_regular", "Help": "question_circle_20_regular", "Expand": "arrow_maximize_20_regular",
    "CheckCircle": "checkmark_circle_20_filled", "DismissCircle": "dismiss_circle_20_filled", "WarningFilled": "warning_20_filled",
    "InfoFilled": "info_20_filled", "LightbulbFilled": "lightbulb_20_filled",
}

TOKEN = re.compile(r"[MmZzLlHhVvCcSsQqTtAa]|[-+]?(?:\d*\.\d+|\d+\.?)(?:[eE][-+]?\d+)?")

def normalize(d):
    out, i, toks, cmd = [], 0, [], None
    # Tokenisation spéciale pour les arcs : les drapeaux (0/1) peuvent être collés
    pos = 0
    res = []
    while pos < len(d):
        c = d[pos]
        if c.isspace() or c == ',':
            pos += 1; continue
        if c.isalpha():
            cmd = c; res.append(c); pos += 1; argi = 0; continue
        if cmd in 'Aa' and (argi % 7) in (3, 4):
            res.append(c); pos += 1; argi += 1; continue
        m = TOKEN.match(d, pos)
        res.append(m.group(0)); pos = m.end(); argi += 1
    return " ".join(res)

def main(src):
    lines = ['<ResourceDictionary xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"',
             '                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">',
             '    <!-- Généré par tools/gen_icons.py — Fluent UI System Icons (Microsoft, licence MIT), grille 20×20. Ne pas éditer à la main. -->']
    for key, name in ICONS.items():
        svg = open(os.path.join(src, name + ".svg")).read()
        paths = re.findall(r'<path[^>]* d="([^"]+)"', svg)
        d = " ".join(normalize(p) for p in paths)
        lines.append(f'    <Geometry x:Key="Icon.{key}">F1 {d}</Geometry>')
    lines.append('</ResourceDictionary>')
    open("src/Maquereaux.App/Themes/Icons.xaml", "w").write("\n".join(lines) + "\n")
    print(len(ICONS), "icônes")

main(sys.argv[1])
