"""
Genera los sprites del juego. Cada objeto se dibuja con sus proporciones reales en el mundo
(ancho x alto en unidades de Unity) y se guarda en una imagen CUADRADA: al ponerla en un objeto
con su Scale actual, Unity la estira de vuelta a las proporciones correctas.
Importar en Unity con Pixels Per Unit = ancho de la imagen (512 o 1024) para que mida 1 unidad.
"""
import math
import random
from PIL import Image, ImageDraw, ImageFilter

SS = 3  # supermuestreo para bordes suaves
random.seed(7)


def lienzo(ancho_px, alto_px):
    img = Image.new("RGBA", (ancho_px * SS, alto_px * SS), (0, 0, 0, 0))
    return img, ImageDraw.Draw(img)


def S(*v):
    return [x * SS for x in v]


def guardar(img, ancho_px, alto_px, lado, nombre):
    img = img.resize((ancho_px, alto_px), Image.LANCZOS)
    img = img.resize((lado, lado), Image.LANCZOS)  # aplastar a cuadrado (Unity lo estira con Scale)
    img.save(f"/home/claude/sprites/{nombre}.png")
    print("ok", nombre, lado)


def mezclar(c1, c2, t):
    return tuple(int(a + (b - a) * t) for a, b in zip(c1, c2))


# ---------------------------------------------------------------- TOSTADA (0.3 x 0.2)
def tostada():
    W, H = 600, 400
    img, d = lienzo(W, H)
    corteza = (146, 86, 38, 255)
    miga = (236, 186, 104, 255)
    tostado = (214, 146, 66, 255)
    # silueta de rebanada: rectángulo con "lomo" redondeado arriba
    d.rounded_rectangle(S(20, 120, 580, 385), radius=60 * SS, fill=corteza)
    d.ellipse(S(20, 20, 300, 230), fill=corteza)
    d.ellipse(S(300, 20, 580, 230), fill=corteza)
    d.rounded_rectangle(S(48, 140, 552, 357), radius=44 * SS, fill=miga)
    d.ellipse(S(48, 48, 300, 230), fill=miga)
    d.ellipse(S(300, 48, 552, 230), fill=miga)
    # tostado más oscuro en el centro
    centro = Image.new("RGBA", img.size, (0, 0, 0, 0))
    dc = ImageDraw.Draw(centro)
    dc.rounded_rectangle(S(110, 110, 490, 320), radius=70 * SS, fill=tostado)
    centro = centro.filter(ImageFilter.GaussianBlur(28 * SS))
    mascara = Image.new("L", img.size, 0)
    ImageDraw.Draw(mascara).rounded_rectangle(S(48, 48, 552, 357), radius=44 * SS, fill=255)
    img.paste(centro, (0, 0), Image.composite(centro, Image.new("RGBA", img.size), mascara))
    d = ImageDraw.Draw(img)
    # poros
    for _ in range(26):
        x, y = random.randint(90, 510), random.randint(90, 320)
        r = random.randint(5, 11)
        d.ellipse(S(x - r, y - r // 2, x + r, y + r // 2), fill=(196, 128, 56, 255))
    # brillo de mantequilla
    d.rounded_rectangle(S(170, 150, 360, 200), radius=25 * SS, fill=(255, 226, 140, 200))
    guardar(img, W, H, 512, "tostada")


# ---------------------------------------------------------------- PLATAFORMA (0.6 x 0.08): tostada asomando
def plataforma():
    W, H = 600, 80
    img, d = lienzo(W, H)
    d.rounded_rectangle(S(0, 0, 600, 80), radius=34 * SS, fill=(146, 86, 38, 255))
    d.rounded_rectangle(S(14, 14, 586, 80), radius=26 * SS, fill=(236, 186, 104, 255))
    d.rounded_rectangle(S(80, 26, 520, 80), radius=20 * SS, fill=(214, 146, 66, 255))
    guardar(img, W, H, 512, "plataforma")


# ---------------------------------------------------------------- TOSTADORA / TOSTI (1 x 0.8)
def tostadora():
    W, H = 1000, 800
    img, d = lienzo(W, H)
    cuerpo = (96, 190, 178, 255)
    sombra = (66, 150, 140, 255)
    brillo = (170, 230, 220, 255)
    cromo = (205, 212, 220, 255)
    oscuro = (40, 48, 56, 255)
    # patas
    for x in (170, 770):
        d.rounded_rectangle(S(x, 700, x + 70, 790), radius=18 * SS, fill=oscuro)
    # cuerpo
    d.rounded_rectangle(S(40, 60, 960, 730), radius=150 * SS, fill=sombra)
    d.rounded_rectangle(S(40, 40, 960, 700), radius=150 * SS, fill=cuerpo)
    d.rounded_rectangle(S(110, 80, 520, 150), radius=35 * SS, fill=brillo)
    # franja cromada
    d.rounded_rectangle(S(40, 470, 960, 540), radius=10 * SS, fill=cromo)
    # ranura arriba (de donde sale la tostada)
    d.rounded_rectangle(S(190, 0, 810, 70), radius=30 * SS, fill=oscuro)
    # palanca a la derecha
    d.rounded_rectangle(S(930, 230, 1000, 300), radius=20 * SS, fill=oscuro)
    d.rounded_rectangle(S(900, 160, 960, 420), radius=25 * SS, fill=sombra)
    # cara de Tosti
    for x in (330, 670):
        d.ellipse(S(x - 70, 230, x + 70, 370), fill=(255, 255, 255, 255))
        d.ellipse(S(x - 30, 270, x + 40, 350), fill=oscuro)
        d.ellipse(S(x - 5, 280, x + 20, 305), fill=(255, 255, 255, 255))
    d.arc(S(400, 320, 600, 450), start=20, end=160, fill=oscuro, width=22 * SS)
    # mejillas
    for x in (230, 770):
        d.ellipse(S(x - 45, 380, x + 45, 430), fill=(240, 130, 120, 170))
    # perilla
    d.ellipse(S(450, 575, 550, 675), fill=cromo)
    d.rounded_rectangle(S(492, 585, 508, 625), radius=6 * SS, fill=oscuro)
    guardar(img, W, H, 512, "tostadora")


# ---------------------------------------------------------------- TOSTI (avatar UI, cuadrado)
def tosti_avatar():
    W = H = 512
    img, d = lienzo(W, H)
    d.rounded_rectangle(S(0, 0, 512, 512), radius=90 * SS, fill=(255, 214, 120, 255))
    # tostadora pequeña con cara
    d.rounded_rectangle(S(56, 130, 456, 470), radius=90 * SS, fill=(96, 190, 178, 255))
    d.rounded_rectangle(S(150, 96, 362, 150), radius=24 * SS, fill=(40, 48, 56, 255))
    d.rounded_rectangle(S(176, 40, 336, 130), radius=28 * SS, fill=(236, 186, 104, 255))
    d.rounded_rectangle(S(176, 40, 336, 70), radius=20 * SS, fill=(146, 86, 38, 255))
    for x in (190, 322):
        d.ellipse(S(x - 42, 220, x + 42, 304), fill=(255, 255, 255, 255))
        d.ellipse(S(x - 16, 244, x + 26, 292), fill=(40, 48, 56, 255))
    d.arc(S(196, 300, 316, 390), start=20, end=160, fill=(40, 48, 56, 255), width=16 * SS)
    guardar(img, W, H, 256, "tosti")


# ---------------------------------------------------------------- TARRO DE MERMELADA (0.5 x 0.45)
def tarro():
    W, H = 500, 450
    img, d = lienzo(W, H)
    vidrio = (210, 235, 245, 230)
    mermelada = (196, 30, 58, 255)
    mermelada2 = (232, 70, 90, 255)
    # frasco
    d.rounded_rectangle(S(40, 110, 460, 445), radius=70 * SS, fill=vidrio)
    d.rounded_rectangle(S(62, 150, 438, 425), radius=56 * SS, fill=mermelada)
    d.rounded_rectangle(S(90, 175, 160, 380), radius=30 * SS, fill=mermelada2)
    # tapa de tela a cuadros
    d.rounded_rectangle(S(20, 40, 480, 130), radius=26 * SS, fill=(255, 255, 255, 255))
    for i in range(12):
        x = 20 + i * 40
        if i % 2 == 0:
            d.rectangle(S(x, 40, min(x + 40, 480), 85), fill=(214, 44, 52, 255))
        else:
            d.rectangle(S(x, 85, min(x + 40, 480), 130), fill=(214, 44, 52, 255))
    d.rounded_rectangle(S(40, 118, 460, 140), radius=10 * SS, fill=(170, 120, 60, 255))
    # etiqueta con fresa
    d.rounded_rectangle(S(150, 240, 350, 360), radius=30 * SS, fill=(255, 246, 220, 255))
    d.polygon(S(250, 335, 215, 290, 225, 270, 250, 278, 275, 270, 285, 290), fill=(214, 44, 52, 255))
    d.polygon(S(232, 268, 250, 255, 268, 268, 250, 276), fill=(80, 160, 70, 255))
    # brillo del vidrio
    d.rounded_rectangle(S(380, 170, 410, 300), radius=14 * SS, fill=(255, 255, 255, 140))
    guardar(img, W, H, 512, "tarro_mermelada")


# ---------------------------------------------------------------- PARED (0.4 x 4.6): azulejos
def pared():
    W, H = 200, 2300
    img, d = lienzo(W, H)
    d.rectangle(S(0, 0, W, H), fill=(196, 188, 176, 255))  # junta
    lado = 100
    for fila in range(0, H // lado + 1):
        for col in range(0, W // lado + 1):
            x, y = col * lado, fila * lado
            tono = (246, 244, 238, 255) if (fila + col) % 2 == 0 else (226, 238, 244, 255)
            d.rounded_rectangle(S(x + 5, y + 5, x + lado - 5, y + lado - 5), radius=8 * SS, fill=tono)
            d.rectangle(S(x + 12, y + 12, x + 40, y + 20), fill=(255, 255, 255, 255))
    # borde superior
    d.rectangle(S(0, 0, W, 30), fill=(120, 140, 160, 255))
    guardar(img, W, H, 1024, "pared")


# ---------------------------------------------------------------- SUELO (10 x 0.6): mesón de madera
def suelo():
    W, H = 5000, 300
    img, d = lienzo(W, H)
    d.rectangle(S(0, 0, W, H), fill=(150, 100, 58, 255))
    # tablones
    x = 0
    while x < W:
        ancho = random.randint(380, 620)
        tono = mezclar((150, 100, 58), (176, 122, 72), random.random())
        d.rectangle(S(x, 40, x + ancho - 6, H), fill=tono + (255,))
        for _ in range(4):  # vetas
            yv = random.randint(70, 280)
            d.line(S(x + 20, yv, x + ancho - 30, yv + random.randint(-10, 10)),
                   fill=(126, 82, 46, 255), width=3 * SS)
        x += ancho
    # canto superior del mesón
    d.rectangle(S(0, 0, W, 44), fill=(210, 160, 104, 255))
    d.rectangle(S(0, 36, W, 48), fill=(118, 76, 42, 255))
    guardar(img, W, H, 1024, "suelo")


# ---------------------------------------------------------------- FONDO DE COCINA (9 x 5)
def fondo():
    W, H = 1800, 1000
    img, d = lienzo(W, H)
    # pared con degradado
    for y in range(H):
        d.line(S(0, y, W, y), fill=mezclar((255, 241, 210), (247, 222, 180), y / H) + (255,), width=SS)
    # salpicadero de azulejos en la parte baja
    for fila in range(6):
        for col in range(0, W // 60 + 1):
            x, y = col * 60, 640 + fila * 60
            tono = (236, 246, 250, 255) if (fila + col) % 2 == 0 else (214, 232, 242, 255)
            d.rectangle(S(x + 2, y + 2, x + 58, y + 58), fill=tono)
    # ventana
    d.rounded_rectangle(S(700, 90, 1180, 470), radius=20 * SS, fill=(250, 250, 250, 255))
    d.rectangle(S(725, 115, 1155, 445), fill=(150, 210, 245, 255))
    d.ellipse(S(1020, 150, 1110, 240), fill=(255, 230, 120, 255))
    for cx, cy in ((830, 210), (900, 190), (960, 220)):
        d.ellipse(S(cx - 60, cy - 28, cx + 60, cy + 28), fill=(255, 255, 255, 255))
    d.rectangle(S(935, 115, 945, 445), fill=(250, 250, 250, 255))
    d.rectangle(S(725, 275, 1155, 285), fill=(250, 250, 250, 255))
    # repisa con frascos
    d.rounded_rectangle(S(150, 330, 560, 350), radius=6 * SS, fill=(150, 100, 58, 255))
    for i, color in enumerate(((214, 44, 52), (240, 170, 50), (90, 170, 90))):
        x = 190 + i * 120
        d.rounded_rectangle(S(x, 240, x + 80, 330), radius=14 * SS, fill=color + (255,))
        d.rectangle(S(x - 4, 228, x + 84, 248), fill=(250, 250, 250, 255))
    # reloj
    d.ellipse(S(1380, 120, 1560, 300), fill=(250, 250, 250, 255))
    d.ellipse(S(1395, 135, 1545, 285), fill=(96, 190, 178, 255))
    d.line(S(1470, 210, 1470, 160), fill=(40, 48, 56, 255), width=8 * SS)
    d.line(S(1470, 210, 1510, 230), fill=(40, 48, 56, 255), width=8 * SS)
    guardar(img, W, H, 1024, "fondo_cocina")


for f in (tostada, plataforma, tostadora, tosti_avatar, tarro, pared, suelo, fondo):
    f()
