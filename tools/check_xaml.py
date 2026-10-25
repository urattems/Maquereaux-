#!/usr/bin/env python3
"""Vérifie les références StaticResource des XAML de l'appli (impossible de lancer WPF sous Linux).
- Dans un dictionnaire de Themes/ : la clé doit être définie AVANT, dans le même fichier (les dictionnaires voisins
  ne sont pas visibles au chargement).
- Dans une vue : la clé doit exister dans un dictionnaire de l'appli, ou dans le fichier lui-même.
Usage : python3 tools/check_xaml.py   (code de sortie 1 en cas d'erreur)"""
import re, glob, sys, os
ROOT = "src/Maquereaux.App"
KEY = re.compile(r'x:Key="([^"]+)"')
REF = re.compile(r'\{StaticResource ([^}\s]+)\}')
errors = []
COMMENT = re.compile(r"<!--.*?-->", re.S)
def read(f):
    # Les commentaires sont remplacés par autant de retours à la ligne (numéros de ligne conservés)
    s = open(f, encoding="utf-8").read()
    return COMMENT.sub(lambda m: "\n" * m.group(0).count("\n"), s)
app = read(f"{ROOT}/App.xaml")
global_keys = set(KEY.findall(app))
themes = sorted(glob.glob(f"{ROOT}/Themes/*.xaml"))
for f in themes:
    s = read(f)
    global_keys |= set(KEY.findall(s))
    defined = set()
    # parcours séquentiel : définitions et références dans l'ordre du fichier
    for m in re.finditer(r'x:Key="([^"]+)"|\{StaticResource ([^}\s]+)\}', s):
        if m.group(1): defined.add(m.group(1))
        elif m.group(2) not in defined and not m.group(2).startswith("{"):
            line = s[:m.start()].count("\n") + 1
            errors.append(f"{f}:{line}: StaticResource '{m.group(2)}' non défini avant dans ce dictionnaire")
for f in glob.glob(f"{ROOT}/Views/*.xaml") + [f"{ROOT}/App.xaml"]:
    s = read(f)
    local = set(KEY.findall(s))
    for m in REF.finditer(s):
        k = m.group(1)
        if k not in global_keys and k not in local:
            line = s[:m.start()].count("\n") + 1
            errors.append(f"{f}:{line}: StaticResource '{k}' introuvable")
for e in errors: print(e)
print(f"{len(errors)} erreur(s)")
sys.exit(1 if errors else 0)
