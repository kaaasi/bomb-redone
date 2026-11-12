"""Findings of docs/analyse/runde.md, checked by 'runde.py befunde'.

Each finding is a function (R, g, args) -> (ok, location); R is the runde module. Checks match instruction sequences
of our own disassembly (regular expressions over ndisasm text built from targets and operands) - no bytes of the
originals are stored here.
"""
FINDINGS = []


def finding(fid, claim):
    def deco(fn):
        FINDINGS.append((fid, claim, fn))
        return fn
    return deco


RND = r"call 0x12ce:0x1675$"
RREAL = r"call 0x12ce:0x168b$"
RCMP = r"call 0x12ce:0x11d8$"
MOVE = r"call 0x12ce:0xa81$"


def imm(n):
    return r"mov ax,0x%x$" % n


# ---- 1. Game sequence (O3[0]) and record order (#71) ------------------------------------------------------------

@finding("S1", "Spielfolge O3[0]: je Spiel Mischen O3:2417, Runde O4[0], Autosave, Wertung, Sichern, Teams, Einkauf, GameNo+1")
def s1(R, g, a):
    return R.seq(g, a, "O3", 0x7847, 0x7A7D, [
        r"call 0x2417$", r"call 0xb0c:0x20$", r"cmp word \[0x47e0\],byte \+0x0$",
        r"cmp byte \[0x4818\],0x0$", r"call 0x3b54$", r"call 0x5aef$", r"cmp byte \[0x4816\],0x0$", r"call 0x46de$",
        r"cmp byte \[0xe8bc\],0x0$", r"call 0x6696$", r"call 0x46de$", r"mov ax,\[0x47e2\]$", r"cmp ax,\[0xe8a6\]$",
        r"call 0xe1f$", r"inc word \[0x47e2\]$", r"cmp ax,\[0xe8a6\]$"])


@finding("S2", "Siegerbonus: Mitglieder des Teams DS:47E0 erhalten 1000·Spielerzahl div Anzahl Punkte, lebende +1 Sieg (+6Ah)")
def s2(R, g, a):
    return R.seq(g, a, "O3", 0x7885, 0x796B, [
        r"mov ax,\[di\+0x4094\]$", r"inc word \[bp-0x4\]$", r"mov ax,\[di\+0x4094\]$", r"cmp byte \[di\+0x4051\],0x0$",
        r"inc word \[di\+0x4098\]$", r"mov ax,0x3e8$", r"mul word \[0x47da\]$", r"idiv word \[bp-0x4\]$",
        r"mov ax,\[di\+0x4041\]$", r"add ax,cx$", r"adc dx,bx$", r"mov \[di\+0x4041\],cx$"])


@finding("S3", "Mischen vor jedem Spiel O3:2417: Random(20)+10 Tausche, je Paar Random(N)+1 zweimal bis verschieden, Tausch O3:0000")
def s3(R, g, a):
    return R.seq(g, a, "O3", 0x2417, 0x2474, [
        imm(0x14), RND, r"add ax,0xa$", r"push word \[0x47da\]$", RND, r"inc ax$", r"push word \[0x47da\]$", RND,
        r"inc ax$", r"cmp ax,\[bp-0x6\]$", r"jz 0x2449$", r"call 0x0$"])


@finding("S4", "Tausch O3:0000: Satz (0x74 Byte, über Satz 0), Nummer O10:0161, Intelligenz DS:3BE8 mit 0129:01BF, Steuerart DS:E5A6")
def s4(R, g, a):
    return R.all_of(
        R.seq(g, a, "O3", 0x0000, 0x012E, [
            r"mov ax,0x74$", MOVE, MOVE, MOVE, r"call 0xb20:0x2f$", r"call 0xb20:0x2f$",
            r"mov ax,\[di\+0x3be8\]$", r"mov \[0x3be8\],ax$", r"mov \[di\+0x3be8\],ax$", r"mov \[di\+0x3be8\],ax$",
            r"call 0x129:0x1bf$", r"call 0x129:0x1bf$", r"mov ax,\[di-0x1a5a\]$", r"mov \[0xe5a6\],ax$",
            r"mov \[di-0x1a5a\],ax$", r"mov \[di-0x1a5a\],ax$", r"retf 0x4$"]),
        R.seq(g, a, "O10", 0x0161, 0x0171, [r"mov al,\[bp\+0xa\]$", r"les di,\[bp\+0x6\]$", r"mov \[es:di\],al$"]),
        R.seq(g, a, "0129", 0x01BF, 0x01D0, [r"mov ax,\[bp\+0xa\]$", r"mov \[es:di\+0x10\],ax$"]))


@finding("S5", "Wertung O3:5AEF sortiert dauerhaft: Blasensortierung nach Siegen (+6Ah), dann bei Gleichstand nach Punkten (+13h)")
def s5(R, g, a):
    return R.seq(g, a, "O3", 0x5B61, 0x5C70, [
        r"mov cx,\[di\+0x4098\]$", r"mov ax,\[di\+0x4098\]$", r"cmp ax,cx$", r"jnl 0x5bbd$", r"call 0x0$",
        r"mov cx,\[di\+0x4098\]$", r"mov ax,\[di\+0x4098\]$", r"cmp ax,cx$", r"jnz 0x5c5b$",
        r"mov cx,\[di\+0x4041\]$", r"mov bx,\[di\+0x4043\]$", r"cmp dx,bx$", r"jl 0x5c4f$", r"call 0x0$"])


# ---- 2. Round start (O4[0] = O4:44A7) ----------------------------------------------------------------------------

@finding("R1", "Rundenbeginn O4:44A7: 0431:0006, Tastatur 0C48:0227, Spielfeld O8[0], Wind, Kopfzeile, Waffenleiste O8[1], Startspieler")
def r1(R, g, a):
    return R.seq(g, a, "O4", 0x44A7, 0x4770, [
        r"call 0x431:0x6$", r"call 0xc48:0x227$", r"call 0xe26:0x1496$", r"call 0xb1a:0x20$", r"mov ax,\[0xe8a8\]$",
        r"call 0xe26:0x1f12$", r"call 0xb1a:0x25$", r"push word \[0x47da\]$", RND])


@finding("R2", "Wind: Option 5 → Random(5); Spanne 0/20/100/300/4000; Wert = Spanne div 2 − Random(Spanne) bis |Wert| ≥ Spanne div 10")
def r2(R, g, a):
    return R.seq(g, a, "O4", 0x44CC, 0x4559, [
        r"cmp word \[0xe8a8\],byte \+0x5$", imm(5), RND, r"mov \[0x47e4\],ax$", r"mov \[0x4742\],ax$",
        r"mov word \[0x4742\],0x14$", r"mov word \[0x4742\],0x64$", r"mov word \[0x4742\],0x12c$",
        r"mov word \[0x4742\],0xfa0$", r"push word \[0x4742\]$", RND, r"mov cx,0x2$", r"idiv cx$",
        r"sub ax,\[0x4740\]$", r"mov \[0xe8a2\],ax$", r"mov cx,0xa$", r"idiv cx$", r"cmp ax,cx$", r"jl 0x44e5$"])


@finding("R3", "Wind: viermal Random:Real (< 0,1; < 0,1; < 0,05; < 0,05) → Wert × 4, danach begrenzt auf ±30000")
def r3(R, g, a):
    return R.all_of(
        R.seq(g, a, "O4", 0x4559, 0x45F1, [
            RREAL, RCMP, r"jnc 0x4578$", r"shl ax,1$", r"shl ax,1$", RREAL, RCMP, r"shl ax,1$", RREAL, RCMP,
            r"shl ax,1$", RREAL, RCMP, r"shl ax,1$", r"cmp word \[0xe8a2\],0x7530$", r"mov word \[0xe8a2\],0x7530$",
            r"cmp word \[0xe8a2\],0x8ad0$", r"mov word \[0xe8a2\],0x8ad0$"]),
        R.real_at(g, a, "O4", 0x455E, 0.1), R.real_at(g, a, "O4", 0x457D, 0.1),
        R.real_at(g, a, "O4", 0x459C, 0.05), R.real_at(g, a, "O4", 0x45BB, 0.05))


@finding("R4", "Startspieler: Random(N) wiederholt, bis Satz s+1 das kleinste Feld +72h hat; danach +72h+1, Durchgang 1")
def r4(R, g, a):
    return R.seq(g, a, "O4", 0x46FF, 0x4794, [
        r"push word \[0x47da\]$", RND, r"mov \[bp-0x2\],ax$", r"mov cx,\[di\+0x40a0\]$", r"mov ax,\[di\+0x40a0\]$",
        r"cmp ax,cx$", r"jnl 0x474c$", r"mov byte \[0x4751\],0x0$", r"jz 0x46ff$", r"inc word \[di\+0x40a0\]$",
        r"mov word \[0x47ea\],0x1$"])


# ---- 3. Playfield (O8) ---------------------------------------------------------------------------------------------

@finding("P1", "Wände der Runde: Option DS:E8AA, bei 5 Random(5); DS:4814 = Wand in {1,2,3}, DS:4815 = Wand 4 (Warp)")
def p1(R, g, a):
    return R.seq(g, a, "O8", 0x0984, 0x09EF, [
        r"mov ax,\[0xe8aa\]$", r"cmp word \[0x47ec\],byte \+0x5$", imm(5), RND, r"mov \[0x47ec\],ax$",
        r"mov di,0x904$", r"call 0x12ce:0xe02$", r"mov byte \[0x4814\],0x1$", r"cmp word \[0x47ec\],byte \+0x4$",
        r"mov byte \[0x4815\],0x1$"])


@finding("P2", "Hintergrund: Option ≤ 8 direkt, 9 → Random(9), 10 (USER) → Random(9) bis Bit in DS:E8AE; Zeichnen O8:0000(n)")
def p2(R, g, a):
    return R.seq(g, a, "O8", 0x0A28, 0x0A7D, [
        r"cmp word \[0xe8ac\],byte \+0x8$", r"push word \[0xe8ac\]$", r"call 0x0$", r"cmp word \[0xe8ac\],byte \+0x9$",
        imm(9), RND, r"call 0x0$", r"cmp word \[0xe8ac\],byte \+0xa$", imm(9), RND, r"shl ax,cl$",
        r"and ax,\[0xe8ae\]$", r"jng 0xa58$", r"call 0x0$"])


@finding("P3", "Hintergründe O8:0000: 1 Stars (Random(500)+250 Sterne, je x=Random(299)+11, y=Random(159)+31, Farbe Random(20)+30)")
def p3(R, g, a):
    return R.seq(g, a, "O8", 0x0000, 0x0163, [
        r"mov ax,0xfa00$", r"call 0x12ce:0x1ed4$", r"cmp ax,0x1$", imm(0x1F4), RND, r"add ax,0xfa$", imm(0x12B), RND,
        r"add ax,0xb$", imm(0x9F), RND, r"add ax,0x1f$", imm(0x14), RND, r"add ax,0x1e$", r"call 0xe26:0x1fe4$"])


@finding("P4", "Hintergründe 2, 3, 4, 6, 7, 8 rufen zuerst 0431:000C (3 × 3 Random(64), Verlaufspalette 30..59); 5 Sky und 1 nicht")
def p4(R, g, a):
    return R.all_of(
        R.seq(g, a, "0431", 0x000C, 0x0052, [imm(0x40), RND, r"cmp word \[bp-0x16\],byte \+0x2$",
                                            r"cmp word \[bp-0x14\],byte \+0x2$"]),
        R.seq(g, a, "O8", 0x0163, 0x06D4, [
            r"cmp ax,0x2$", r"call 0x431:0xc$", r"cmp ax,0x3$", r"call 0x431:0xc$", r"cmp ax,0x4$", r"call 0x431:0xc$",
            r"cmp ax,0x5$", r"call far \[0xe8f6\]$", r"cmp ax,0x6$", r"call 0x431:0xc$", r"cmp ax,0x7$",
            r"call 0x431:0xc$", r"cmp ax,0x8$", r"call 0x431:0xc$"]))


@finding("P5", "Hintergrund 6 Static: je Pixel des Fensters (x außen, y innen) Random(30)+30; 7 Rectangle: Random(3) → 80/150/159 Rahmen")
def p5(R, g, a):
    return R.all_of(
        R.seq(g, a, "O8", 0x052C, 0x05E9, [r"mov ax,\[0xe8fa\]$", r"mov \[0x4746\],ax$", r"mov ax,\[0xe8fc\]$",
                                           r"mov \[0x4748\],ax$", imm(0x1E), RND, r"add ax,0x1e$", r"call 0xcc8:0xbb6$"]),
        R.seq(g, a, "O8", 0x05E9, 0x06AA, [imm(3), RND, r"mov word \[0x474c\],0x50$", r"mov word \[0x474c\],0x96$",
                                           r"mov word \[0x474c\],0x9f$", r"call 0xcc8:0x31$"]))


@finding("P6", "Hintergrund-Ende (alle, auch 0): Bild 10..310 × 30..190 in Puffer DS:E5C0, Rahmen Farbe 13, Random:Real < 0,5 → 0C79:0428")
def p6(R, g, a):
    return R.all_of(
        R.seq(g, a, "O8", 0x06AA, 0x0905, [r"jmp 0x877$", r"les di,\[0xe5c0\]$", r"mov \[0xe904\],ax$",
                                           r"mov word \[0x4746\],0xa$", r"mov dx,0xa000$", r"call 0xcc8:0xac0$",
                                           r"cmp word \[0x4746\],0x136$", r"mov al,0xd$", r"call 0xcc8:0x31$",
                                           r"mov al,0xd$", r"call 0xcc8:0x31$", r"mov word \[0xe904\],0xa000$",
                                           RREAL, RCMP, r"jnc 0x8fe$", r"call 0xc79:0x428$"]),
        R.real_at(g, a, "O8", 0x08EB, 0.5))


@finding("P7", "Wandbeschriftung links senkrecht (SmallFont): STICKEY, ELASTIC, ACCELERATE, WARP; Faktor DS:47D2 = 0,707106 / 1 / 1,41421")
def p7(R, g, a):
    return R.all_of(
        R.seq(g, a, "O8", 0x0AFF, 0x0BBF, [r"cmp ax,0x1$", r"mov di,0x924$", r"call 0xe26:0x1f12$",
                                           r"mov word \[0x47d2\],0xce80$", r"cmp ax,0x2$", r"mov di,0x92c$",
                                           r"cmp ax,0x3$", r"mov di,0x934$", r"cmp ax,0x4$", r"mov di,0x93f$"]),
        R.real_at(g, a, "O8", 0x0B22, 0.707106), R.real_at(g, a, "O8", 0x0B57, 1.0),
        R.real_at(g, a, "O8", 0x0B8B, 1.41421))


@finding("P8", "Gelände Anfang: Farbe 2, x = 11, Steigung 0, Richtung 1 − 2·Random(2), Rand 60 − 7·(N−2), Höhe Random(150)")
def p8(R, g, a):
    return R.seq(g, a, "O8", 0x0BBF, 0x0C35, [
        r"call 0xe26:0x1dc2$", r"mov word \[0x4746\],0xb$", r"mov \[bp-0x4\],ax$", imm(2), RND, r"shl ax,1$",
        r"sub ax,cx$", r"mov \[bp-0x2\],ax$", r"mov ax,\[0x47da\]$", r"mov dx,0x7$", r"mul dx$", r"mov ax,0x3c$",
        r"sub ax,dx$", r"mov \[bp-0x6\],ax$", imm(0x96), RND, r"mov \[0x473e\],ax$"])


@finding("P9", "Gelände je Schritt bis x = 310: Rand flach (Line), sonst |Steigung| 0..4 → Spalten 3/3/3/2/1 und Höhenwechsel 0/1/2/2/2")
def p9(R, g, a):
    return R.seq(g, a, "O8", 0x0C35, 0x0EA6, [
        r"cmp word \[0x4746\],0x136$", r"add ax,0xb$", r"call 0xe26:0x1c86$", r"mov ax,0x135$", r"sub ax,\[bp-0x6\]$",
        r"call 0xe26:0x1c86$", r"cmp word \[bp-0x4\],byte \+0x0$", r"call 0xcc8:0x9e4$", r"call 0xcc8:0x9e4$",
        r"call 0xcc8:0x9e4$", r"cmp ax,0x1$", r"cmp ax,0x2$", r"cmp ax,0x3$", r"cmp ax,0x4$"])


@finding("P10", "Gelände: Random:Real < 0,2 → Steigung += Richtung; Random:Real < 0,1 oder |Steigung| > 4 → Richtung umkehren; Grenzen")
def p10(R, g, a):
    return R.all_of(
        R.seq(g, a, "O8", 0x0EA6, 0x0F3D, [
            RREAL, RCMP, r"jnc 0xec4$", r"add ax,\[bp-0x2\]$", RREAL, RCMP, r"jc 0xee6$", r"cmp ax,0x4$", r"neg ax$",
            r"cmp word \[bp-0x4\],byte \+0x4$", r"mov word \[bp-0x4\],0x4$", r"cmp word \[bp-0x4\],byte -0x4$",
            r"mov word \[bp-0x4\],0xfffc$", r"cmp word \[0x473e\],byte \+0x14$", r"mov word \[bp-0x2\],0x1$",
            r"cmp word \[0x473e\],0x8c$", r"mov word \[bp-0x2\],0xffff$", r"cmp word \[0x473e\],byte \+0x3$",
            r"mov word \[bp-0x4\],0x1$", r"cmp word \[0x473e\],0x9b$", r"mov word \[bp-0x4\],0xffff$",
            r"inc word \[0x4746\]$", r"jmp 0xc35$"]),
        R.real_at(g, a, "O8", 0x0EAB, 0.2), R.real_at(g, a, "O8", 0x0EC9, 0.1))


@finding("P11", "Panzer x: Spieler 1 Random(28)+16, Spieler N 305 − Random(28), dazwischen gleichmäßig (Abstand div N−1)")
def p11(R, g, a):
    return R.seq(g, a, "O8", 0x0F3D, 0x0FC8, [
        imm(0x1C), RND, r"add ax,0x10$", r"mov \[0x40b1\],ax$", imm(0x1C), RND, r"mov ax,0x131$", r"sub ax,dx$",
        r"mov \[di\+0x403d\],cx$", r"sub ax,\[0x40b1\]$", r"idiv cx$", r"mul word \[0x4742\]$", r"add ax,\[0x40b1\]$",
        r"mov \[di\+0x403d\],cx$"])


@finding("P12", "Panzer je Spieler: Name, +1Dh 75 (Spieler 1 und N) sonst 90, +1Bh 200, +1Fh −1 (Spieler N) sonst 1, +23h 1, y 32")
def p12(R, g, a):
    return R.seq(g, a, "O8", 0x0FC8, 0x1114, [
        r"call 0xe26:0x1f12$", r"mov di,0x944$", r"call 0x12ce:0xd8d$", r"call 0x12ce:0xe02$",
        r"mov word \[di\+0x404b\],0x4b$", r"mov word \[di\+0x404b\],0x5a$", r"mov word \[di\+0x4049\],0xc8$",
        r"mov word \[di\+0x404d\],0xffff$", r"mov word \[di\+0x404d\],0x1$", r"mov byte \[di\+0x4051\],0x1$",
        r"mov word \[di\+0x403f\],0x20$", r"mov \[di\+0x408c\],ax$"])


@finding("P13", "Panzer fallen: kein Fall, wenn eines der 9 Pixel x−4..x+4 unter dem Panzer Farbe 2 oder 13; sonst fallen bis alle 9")
def p13(R, g, a):
    return R.seq(g, a, "O8", 0x1114, 0x1251, [
        r"mov byte \[bp-0x7\],0x1$", r"mov word \[0x4742\],0xfffc$", r"call 0xcc8:0xbff$", r"mov di,0x964$",
        r"call 0x12ce:0xe02$", r"jz 0x1159$", r"mov byte \[bp-0x7\],0x0$", r"cmp word \[0x4742\],byte \+0x4$",
        r"mov byte \[bp-0x8\],0x1$", r"call 0x431:0x685$", r"inc word \[di\+0x403f\]$", r"call 0x431:0x5ed$",
        r"push word \[0xe8c8\]$", r"call 0x11f8:0x29c$", r"call 0xcc8:0xbff$", r"call 0x12ce:0xe02$",
        r"jnz 0x1216$", r"mov byte \[bp-0x8\],0x0$", r"jmp 0x116d$"])


@finding("P14", "Randomize in 0000:013B nach O2[0] und O2[3] (BOMB.CFG), vor Maustest, Titel und Hauptmenü O2[2]")
def p14(R, g, a):
    return R.seq(g, a, "0000", 0x013B, 0x0540, [
        r"call 0xb02:0x20$", r"call 0xb02:0x2f$", r"call 0x12ce:0x16fc$", r"call 0xb02:0x25$", r"call 0xb02:0x2a$",
        r"call 0xb08:0x20$"])


@finding("P15", "Vor der ersten Runde: Hauptmenü O2:3B96(0) Random(2); „Random“-Computer Random(7)+1; Intelligenz-Init Random(4)+1 bis passend")
def p15(R, g, a):
    return R.all_of(
        R.seq(g, a, "O2", 0x3B96, 0x3C40, [r"cmp ax,0x0$", imm(2), RND, r"mov word \[0x474c\],0x64$",
                                           r"mov word \[0x474c\],0xa0$"]),
        R.seq(g, a, "O2", 0x3E0A, 0x3E40, [r"xor ax,ax$", r"call 0x3b96$"]),
        R.seq(g, a, "O2", 0x5A60, 0x5AB2, [r"call 0x12ce:0xc52$", imm(7), RND, r"inc ax$", r"call 0x78$"]),
        R.seq(g, a, "0129", 0x0000, 0x00B7, [imm(4), RND, r"inc ax$", r"mov \[es:di\+0xe\],ax$",
                                             r"call 0x12ce:0xe02$", r"jz 0x87$"]))


@finding("P16", "Spielfeld-Ablauf: Rotation aus, SetRGB(19, 63, 63, 63) bzw. bei Inviso (0, 0, 0), Hintergrund, SetRGB(0, 14, 9, 10), Rahmen 13")
def p16(R, g, a):
    return R.seq(g, a, "O8", 0x0984, 0x0AC3, [
        r"mov di,0x3706$", r"mov di,0xa0de$", r"mov ax,0x10$", MOVE, r"call 0xc79:0x3d$", r"mov ax,0x13$",
        r"mov ax,0x3f$", r"mov ax,0x3f$", r"mov ax,0x3f$", r"call far \[0xe8f6\]$", r"cmp byte \[0xe8b4\],0x0$",
        r"mov ax,0x13$", r"call far \[0xe8f6\]$", r"call 0xe26:0xf80$", r"call 0x0$", r"mov ax,0xe$", r"mov ax,0x9$",
        r"mov ax,0xa$", r"call far \[0xe8f6\]$", r"mov ax,0xd$", r"call 0xe26:0x1dc2$", r"mov ax,0x8$",
        r"mov ax,0x1c$", r"mov ax,0x138$", r"mov ax,0xc0$", r"call 0xe26:0x10d2$", r"call 0xe26:0x10d2$"])


@finding("R5", "Erster Zug: Satz s+1 (Index + 1, über N zurück auf 1 mit Durchgang + 1), nur wenn +23h ≠ 0; Nummer nach DS:47EE")
def r5(R, g, a):
    return R.seq(g, a, "O4", 0x4766, 0x47A2, [
        r"inc word \[di\+0x40a0\]$", r"mov word \[0x47ea\],0x1$", r"inc word \[bp-0x2\]$", r"cmp ax,\[0x47da\]$",
        r"mov word \[bp-0x2\],0x1$", r"inc word \[0x47ea\]$", r"cmp byte \[di\+0x4051\],0x0$", r"jz 0x4770$",
        r"mov \[0x47ee\],al$"])


# ---- 4. Shot of the ballistic weapons (O4:01B7) -------------------------------------------------------------------

def set_const(R, g, a, key, ofs):
    """Members of a 32-byte Pascal set constant at offset ofs of block key."""
    lines, data, f, base = R.block_lines(g, a, key)
    b = data[ofs:ofs + 32]
    return sorted(i for i in range(256) if b[i // 8] >> (i % 8) & 1)


def set_is(R, g, a, key, ofs, members):
    lines, data, f, base = R.block_lines(g, a, key)
    got = set_const(R, g, a, key, ofs)
    return got == members, "%s:%04X = {%s}, %s 0x%05X" % (key, ofs, ",".join(map(str, got)), f, base + ofs)


@finding("Q1", "Waffe (+24h) {0..4, 6, 8..17} → O4:01B7 (Flugbahn), 5 Laser → O4:3426, 7 MIRV → O4:13D4; nach dem Zug O7:040C")
def q1(R, g, a):
    return R.all_of(
        R.seq(g, a, "O4", 0x4960, 0x49CB, [r"call 0xb16:0x2f$", r"mov al,\[di\+0x4052\]$", r"mov di,0x4447$",
                                           r"call 0x1b7$", r"mov di,0x4467$", r"call 0x3426$", r"mov di,0x4487$",
                                           r"call 0x13d4$"]),
        set_is(R, g, a, "O4", 0x4447, [0, 1, 2, 3, 4, 6, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17]),
        set_is(R, g, a, "O4", 0x4467, [5]), set_is(R, g, a, "O4", 0x4487, [7]))


@finding("Q2", "Abschuss: Bestand der Waffe −1, Lenkung +26h ∈ {1,3} → +2Ah −1, ∈ {2,3} → +2Ch −1; Random:Real < 0,2 → 1 Schaden am Schützen")
def q2(R, g, a):
    return R.all_of(
        R.seq(g, a, "O4", 0x02E4, 0x0384, [r"mov ax,\[di\+0x4052\]$", r"dec word \[di\+0x405c\]$", r"mov di,0x177$",
                                           r"dec word \[di\+0x4058\]$", r"mov di,0x197$", r"dec word \[di\+0x405a\]$",
                                           RREAL, RCMP, r"jnc 0x384$", r"mov ax,0x1$", r"mov al,0x0$",
                                           r"call 0xb20:0x20$"]),
        set_is(R, g, a, "O4", 0x0177, [1, 3]), set_is(R, g, a, "O4", 0x0197, [2, 3]),
        R.real_at(g, a, "O4", 0x035A, 0.2))


@finding("Q3", "Start: x = x_P + 10·cos(a)·r, y = y_P − 10·sin(a), vx = Kraft/50·cos(a)·r, vy = −Kraft/50·sin(a); a (+1Dh) in Grad, r = +1Fh")
def q3(R, g, a):
    return R.all_of(
        R.seq(g, a, "O4", 0x0384, 0x0515, [
            r"mov ax,\[di\+0x404b\]$", r"call 0xdbd:0xd9$", r"call 0x12ce:0x12ee$", r"call 0x12ce:0x11c8$",
            r"mov ax,\[di\+0x404d\]$", r"call 0x12ce:0x11c8$", r"mov ax,\[di\+0x403d\]$", r"call 0x12ce:0x11b6$",
            r"mov \[0x4796\],ax$", r"call 0x12ce:0x1301$", r"mov ax,\[di\+0x403f\]$", r"call 0x12ce:0x11bc$",
            r"mov \[0x479c\],ax$", r"mov ax,\[di\+0x4049\]$", r"call 0x12ce:0x11ce$", r"call 0x12ce:0x12ee$",
            r"mov ax,\[di\+0x404d\]$", r"mov \[0x47a2\],ax$", r"mov ax,\[di\+0x4049\]$", r"call 0x12ce:0x1301$",
            r"xor dh,0x80$", r"mov \[0x47a8\],ax$"]),
        R.real_at(g, a, "O4", 0x03A3, 10.0), R.real_at(g, a, "O4", 0x045B, 50.0))


@finding("Q4", "Je Teilschritt (SS = DS:E8CA Teilschritte): x += vx/SS, y += vy/SS, vy += 0,1/SS, vx += (Wind/5000)/SS; Ticks je SS")
def q4(R, g, a):
    return R.all_of(
        R.seq(g, a, "O4", 0x0524, 0x073F, [
            r"mov ax,\[0xe8ca\]$", r"mov \[0x4744\],ax$", r"inc word \[0x4740\]$", r"cmp ax,\[0x4744\]$",
            r"add word \[bp-0x2e\],byte \+0x1$", r"mov ax,\[0x47a2\]$", r"call 0x12ce:0x11ce$", r"mov cx,\[0x4796\]$",
            r"call 0x12ce:0x11b6$", r"mov ax,\[0x47a8\]$", r"call 0x12ce:0x11ce$", r"mov cx,\[0x479c\]$",
            r"call 0x12ce:0x11b6$", r"call 0x12ce:0x11ce$", r"mov cx,\[0x47a8\]$", r"call 0x12ce:0x11b6$",
            r"mov \[0x47a8\],ax$", r"mov ax,\[0xe8a2\]$", r"call 0x12ce:0x11ce$", r"call 0x12ce:0x11ce$",
            r"mov cx,\[0x47a2\]$", r"call 0x12ce:0x11b6$", r"mov \[0x47a2\],ax$", r"call 0x12ce:0x11e0$",
            r"call 0x12ce:0x11e0$"]),
        R.real_at(g, a, "O4", 0x06A6, 0.1), R.real_at(g, a, "O4", 0x06E8, 5000.0))


@finding("Q5", "Repulser (+5Ah > 0) eines lebenden Panzers in ±40: d² = dx² + dy² ≤ 1600 → Stoß −600/(d²·SS) in Richtung ArcTan, Linie Farbe 14")
def q5(R, g, a):
    return R.all_of(
        R.seq(g, a, "O4", 0x07B5, 0x0A37, [
            r"cmp word \[di\+0x408a\],byte \+0x0$", r"cmp ax,0x28$", r"cmp ax,0x28$", r"call 0x12ce:0x122a$",
            r"call 0x12ce:0x122a$", r"call 0x12ce:0x11e0$", r"cmp word \[bp-0x22\],0x640$", r"mov al,0xe$",
            r"call 0xcc8:0x9e4$", r"call 0x12ce:0x11ce$", r"call 0x12ce:0x11ce$", r"call 0x12ce:0x14d8$",
            r"call 0xdbd:0x11b$", r"add ax,0xb4$", r"mov word \[bp-0x24\],0x5a$", r"mov word \[bp-0x24\],0x10e$",
            r"call 0xb25:0x5a$", r"mov \[0x47a2\],ax$", r"call 0xb25:0x0$", r"call 0x12ce:0x11bc$",
            r"mov \[0x47a8\],ax$"]),
        R.real_at(g, a, "O4", 0x091A, -600.0))


@finding("Q6", "Wände 1–3: x ≤ 10 bzw. ≥ 310 (Richtung nach außen) → vx = −vx·F, vy·F; y ≤ 30 bzw. ≥ 188 → vy = −vy·F, vx·F (F = DS:47D2)")
def q6(R, g, a):
    return R.all_of(
        R.seq(g, a, "O4", 0x0E5E, 0x1067, [
            r"cmp byte \[0x4814\],0x0$", r"cmp word \[bp-0x18\],byte \+0xa$", r"mov ax,\[0x47a2\]$", RCMP,
            r"jnc 0xedb$", r"xor dh,0x80$", r"mov cx,\[0x47d2\]$", r"mov cx,\[0x47d2\]$",
            r"cmp word \[bp-0x18\],0x136$", r"jna 0xf4f$", r"xor dh,0x80$", r"mov cx,\[0x47d2\]$",
            r"cmp word \[bp-0x1a\],byte \+0x1e$", r"jnc 0xfc2$", r"xor dh,0x80$", r"mov cx,\[0x47d2\]$",
            r"cmp word \[bp-0x1a\],0xbc$", r"ja 0xfe7$", r"mov cx,\[0x47d2\]$", r"xor dh,0x80$",
            r"and dh,0x7f$", RCMP, r"mov byte \[0x4750\],0x1$", r"mov byte \[0x4751\],0x1$"]),
        R.real_at(g, a, "O4", 0x104A, 0.1))


@finding("Q7", "Warp: x ≤ 10 → x + 300, x ≥ 310 → x − 300; keine Wand: rechts (vx > 1, Wind > 0) bzw. links (vx < 1, Wind < 0) ohne Lenkung → y = 300")
def q7(R, g, a):
    return R.all_of(
        R.seq(g, a, "O4", 0x1067, 0x115D, [
            r"cmp byte \[0x4815\],0x0$", r"cmp word \[bp-0x18\],byte \+0xa$", r"call 0x12ce:0x11b6$",
            r"cmp word \[bp-0x18\],0x136$", r"call 0x12ce:0x11bc$", r"cmp byte \[0x4814\],0x0$",
            r"cmp word \[bp-0x18\],0x136$", RCMP, r"jna 0x1116$", r"cmp word \[0xe8a2\],byte \+0x0$", r"jng 0x1116$",
            r"cmp word \[di\+0x4054\],byte \+0x0$", r"mov word \[0x479c\],0x89$", r"cmp word \[bp-0x18\],byte \+0xa$",
            RCMP, r"jnc 0x115d$", r"cmp word \[0xe8a2\],byte \+0x0$", r"jnl 0x115d$", r"mov word \[0x479c\],0x89$"]),
        R.real_at(g, a, "O4", 0x107F, 300.0), R.real_at(g, a, "O4", 0x10E0, 1.0), R.real_at(g, a, "O4", 0x1127, 1.0),
        R.real_at(g, a, "O4", 0x1104, 300.0))


@finding("Q8", "Ende des Flugs: > 1000 Ticks; Wand 0/4: y < −5000 (ohne Einschlag) oder y ≥ 190; |vx| oder |vy| > 30 (mit Einschlag)")
def q8(R, g, a):
    return R.all_of(
        R.seq(g, a, "O4", 0x115D, 0x12DD, [
            r"cmp word \[bp-0x2c\],byte \+0x0$", r"cmp word \[bp-0x2e\],0x3e8$", r"mov byte \[0x4751\],0x1$",
            r"mov byte \[0x4750\],0x1$", r"cmp byte \[0x4814\],0x0$", r"cmp word \[bp-0x1a\],0xec78$",
            r"mov byte \[0x4750\],0x0$", r"cmp word \[bp-0x1a\],0xbe$", r"mov byte \[0x4751\],0x1$",
            r"and dh,0x7f$", RCMP, r"and dh,0x7f$", RCMP, r"mov byte \[0x4751\],0x1$", r"mov byte \[0x4750\],0x1$",
            r"jmp 0x60a$"]),
        R.real_at(g, a, "O4", 0x129D, 30.0))


@finding("Q9", "Treffertest im Feld (11 < x < 309, 33 < y < 187): Raute |dx|+|dy| ≤ 1; Farbe 2 Gelände, Farbe 20..29 Panzer → DS:47DE")
def q9(R, g, a):
    return R.seq(g, a, "O4", 0x0D00, 0x0E5E, [
        r"cmp word \[bp-0x18\],byte \+0xb$", r"cmp word \[bp-0x1a\],byte \+0x21$", r"cmp word \[bp-0x18\],0x135$",
        r"cmp word \[bp-0x1a\],0xbb$", r"mov word \[0x4746\],0xffff$", r"mov word \[0x4748\],0xffff$",
        r"cmp ax,0x1$", r"call 0xcc8:0xbff$", r"cmp word \[bp-0x6\],byte \+0x2$", r"mov \[0x47de\],ax$",
        r"cmp word \[bp-0x6\],byte \+0x14$", r"cmp word \[bp-0x6\],byte \+0x1d$", r"mov \[0x47de\],ax$",
        r"mov byte \[0x4751\],0x1$"])


@finding("Q10", "Einschlag O6:00E0: Lead Ball (Waffe 0) nur Volltreffer: 5 Schaden (O10:0000), 10 Punkte je verlorener Energie an andere Teams")
def q10(R, g, a):
    return R.all_of(
        R.seq(g, a, "O4", 0x1343, 0x1376, [r"cmp byte \[0x4750\],0x0$", r"push word \[0x47de\]$", r"call 0xb13:0x20$"]),
        R.seq(g, a, "O6", 0x00E0, 0x0180, [
            r"cmp word \[di\+0x4052\],byte \+0x0$", r"jnz 0x180$", r"cmp word \[bp\+0x6\],byte \+0x14$",
            r"cmp word \[bp\+0x6\],byte \+0x1d$", r"mov word \[bp-0x14\],0x5$", r"call 0xb20:0x20$", r"mov dx,0xa$",
            r"mul dx$", r"mov ax,\[di\+0x4094\]$", r"cmp ax,cx$", r"jz 0x17d$", r"call 0xb20:0x34$"]))


@finding("Q11", "Schaden O10:0000(d, k, Satz): Abwehr k −= d; darunter Energie +21h (≥ 0); k ≠ 1 mit Repulser: Random:Real < Rest/40 → Repulser −1")
def q11(R, g, a):
    return R.all_of(
        R.seq(g, a, "O10", 0x0000, 0x014A, [
            r"mov ax,\[bp\+0xc\]$", r"mov ax,\[es:di\+0x52\]$", r"sub ax,cx$", r"mov \[es:di\+0x52\],cx$",
            r"mov ax,\[es:di\+0x21\]$", r"cmp word \[es:di\+0x54\],byte \+0x0$", r"add ax,cx$",
            r"mov \[es:di\+0x21\],ax$", r"cmp byte \[bp\+0xa\],0x1$", r"cmp word \[es:di\+0x5c\],byte \+0x0$",
            r"call 0x12ce:0x11ce$", RREAL, RCMP, r"jnc 0x113$", r"sub word \[es:di\+0x5a\],byte \+0x1$",
            r"mov \[es:di\+0x52\],ax$", r"call 0x14a$", r"sub ax,\[es:di\+0x21\]$"]),
        R.real_at(g, a, "O10", 0x00E7, 40.0))


@finding("Q12", "Explosion O6:00E0 für Waffen {1,2,3,4,7,11}: Radius DS:47D8 = 10 / 15 (2, 7) / 30 / 60 / 2 (11); Farben 130.. als Verlauf")
def q12(R, g, a):
    return R.all_of(
        R.seq(g, a, "O6", 0x0180, 0x0250, [
            r"mov di,0x0$", r"call 0x12ce:0xe02$", r"cmp ax,0x1$", r"mov word \[0x47d8\],0xa$", r"cmp ax,0x2$",
            r"cmp ax,0x7$", r"mov word \[0x47d8\],0xf$", r"cmp ax,0x3$", r"mov word \[0x47d8\],0x1e$", r"cmp ax,0x4$",
            r"mov word \[0x47d8\],0x3c$", r"cmp ax,0xb$", r"mov word \[0x47d8\],0x2$", r"mov ax,0x3f$",
            r"idiv word \[0x47d8\]$", r"mov ax,0x82$", r"call far \[0xe8f2\]$"]),
        set_is(R, g, a, "O6", 0x0000, [1, 2, 3, 4, 7, 11]))


@finding("Q13", "Erdrutsch nach dem Schuss O7:03C1: Rahmen neu, Random:Real · 100 ≤ Crumble % → 0431:0496 (Puffer DS:E5C0, DS:DFA2)")
def q13(R, g, a):
    return R.all_of(
        R.seq(g, a, "O7", 0x03C1, 0x040C, [r"call 0x364$", r"mov ax,\[0xe8a4\]$", RREAL, r"call 0x12ce:0x11c8$", RCMP,
                                           r"ja 0x408$", r"call 0x431:0x496$"]),
        R.real_at(g, a, "O7", 0x03DC, 100.0))


@finding("Q14", "Winkel: 12CE:12EE = Cos (Sin(π/2 − x)), 12CE:1301 = Sin, 12CE:122A = Int; 0DBD:00D9 Grad→Bogen, 011B Bogen→Grad; 0B25:0000/005A Sin/Cos-Tabelle")
def q14(R, g, a):
    return R.all_of(
        R.seq(g, a, "0DBD", 0x00D9, 0x015D, [r"mov cx,0x88$", r"call 0x12ce:0x11ce$", r"mov cx,0x2182$",
                                             r"call 0x12ce:0x11c8$", r"retf 0x6$", r"mov cx,0x2182$",
                                             r"call 0x12ce:0x11ce$", r"mov cx,0x88$", r"call 0x12ce:0x11c8$"]),
        R.real_at(g, a, "0DBD", 0x00F5, 3.141592653589793), R.real_at(g, a, "0DBD", 0x00E8, 180.0),
        R.seq(g, a, "12CE", 0x12EE, 0x1310, [r"mov cx,0x2181$", r"mov di,0xc90f$", r"call 0xeb2$", r"xor dh,0x80$",
                                             r"cmp al,0x6c$", r"mov cx,0x2183$"]),
        R.real_at(g, a, "12CE", 0x12EE, -1.5707963267948966),
        R.seq(g, a, "12CE", 0x122A, 0x127B, [r"cmp al,0xa8$", r"sub cl,0x80$", r"rcr dx,1$", r"and dx,di$"]),
        R.seq(g, a, "0B25", 0x0000, 0x00F4, [r"cmp word \[bp\+0x6\],0xb4$", r"cmp word \[bp\+0x6\],byte \+0x5a$",
                                             r"mov bx,0x6$", r"mov ax,\[cs:bx\+0xf9\]$", r"retf 0x2$", r"mov ax,0x5a$",
                                             r"sub ax,\[bp\+0x6\]$", r"mov ax,\[cs:bx\+0xf9\]$"]))


@finding("Q15", "Flug zeichnen: Spurfarbe 100 − i = 32 div Farbteiler, Ton 600 − 2·iy, Punkt 0431:03C3, Rahmenmarken Farbe 13; Explosion bei (x − 11, y − 31)")
def q15(R, g, a):
    return R.all_of(
        R.seq(g, a, "O4", 0x0538, 0x0591, [r"sub ax,\[0x4740\]$", r"mov cx,\[di\+0x408e\]$", r"mov ax,0x20$",
                                           r"idiv cx$", r"mov cx,\[di\+0x4090\]$", r"mov cx,\[di\+0x4092\]$",
                                           r"call far \[0xe8f6\]$"]),
        R.seq(g, a, "O4", 0x0C03, 0x0C82, [r"cmp byte \[0xe8b2\],0x0$", r"shl ax,1$", r"mov ax,0x258$", r"sub ax,cx$",
                                           r"call 0x431:0x40f$", r"cmp byte \[0xe8b5\],0x0$", r"mov ax,0x64$",
                                           r"sub ax,\[bp\+0x6\]$", r"call 0x431:0x56d$"]),
        R.seq(g, a, "O4", 0x11EE, 0x1270, [r"call 0x431:0x3c3$", r"mov ax,0x1d$", r"mov al,0xd$", r"mov ax,0xbf$",
                                           r"mov al,0xd$", r"mov ax,0x9$", r"mov al,0xd$", r"mov ax,0x137$",
                                           r"mov al,0xd$"]),
        R.seq(g, a, "O6", 0x024D, 0x02CA, [r"mov ax,0xb$", r"mov ax,0x1f$", r"call 0xe26:0xf93$",
                                           r"call 0x12ce:0x11bc$", r"call 0x12ce:0x11bc$"]),
        R.real_at(g, a, "O6", 0x0273, 11.0), R.real_at(g, a, "O6", 0x0292, 31.0))


# ---- 5.6 Impact, crater, landslide, falling, death, lightning, round end ------------------------------------------

SOUND = r"cmp byte \[0xe8b3\],0x0$"
DELAY_ES = r"push word \[0xe8c6\]$"
DAMAGE = r"call 0xb20:0x20$"
POINTS = r"call 0xb20:0x34$"


@finding("K1", "Explosion O6:00E0: je Ring nur bei Ton Random(500)+500, drei Ellipsen 0CC8:0351 Farbe 130+i; dann drei Palettenläufe mit Delay(ES)")
def k1(R, g, a):
    return R.seq(g, a, "O6", 0x02CA, 0x0522, [
        SOUND, imm(0x1f4), RND, r"add ax,0x1f4$", r"call 0x11f8:0x2c7$", r"add ax,0x82$", r"call 0xcc8:0x351$",
        r"call 0xcc8:0x351$", r"call 0xcc8:0x351$",
        SOUND, imm(0x1f4), RND, r"add ax,0xfa$", r"call far \[0xe8f2\]$", DELAY_ES,
        SOUND, imm(0x1f4), RND, r"add ax,0x3$", r"call far \[0xe8f2\]$", DELAY_ES,
        SOUND, imm(0xfa), RND, r"add ax,0x3$", r"call far \[0xe8f2\]$", DELAY_ES, r"call 0x11f8:0x2f4$"])


@finding("K2", "Krater: Radius 0..R je drei Ellipsen (x±1, x) mit 0CC8:0687 aus dem Hintergrundpuffer DS:E5C0 (Gelände wird Hintergrund)")
def k2(R, g, a):
    return R.all_of(
        R.seq(g, a, "O6", 0x0522, 0x058E, [r"mov ax,\[0x47d8\]$", r"inc ax$", r"push word \[0xe5c0\]$",
                                           r"call 0xcc8:0x687$", r"dec ax$", r"push word \[0xe5c0\]$",
                                           r"call 0xcc8:0x687$", r"push word \[0xe5c0\]$", r"call 0xcc8:0x687$"]),
        R.seq(g, a, "0CC8", 0x0687, 0x0760, [r"mov ax,\[bp\+0xc\]$", r"mul ax$", r"mov ax,\[bp\+0xa\]$", r"mul ax$",
                                             r"mov ax,0x140$"]))


@finding("K3", "Volltreffer einer Sprengwaffe (Farbe 20..29): 200 Schaden am getroffenen Satz, Schütze 10 Punkte je Energie bei anderem Team")
def k3(R, g, a):
    return R.seq(g, a, "O6", 0x058E, 0x05FC, [
        r"cmp word \[bp\+0x6\],byte \+0x14$", r"cmp word \[bp\+0x6\],byte \+0x1d$", r"mov word \[bp-0x14\],0xc8$",
        DAMAGE, r"mov dx,0xa$", r"mov ax,\[di\+0x4094\]$", r"cmp ax,cx$", POINTS])


@finding("K4", "Explosionsschaden je lebendem Satz: d = Sqrt(dx²+dy²); d ≤ 1,5R: +Trunc(1,8R−d); d < R: +10+2·Trunc(1,8R−d); d < 0,7R: +150")
def k4(R, g, a):
    return R.all_of(
        R.seq(g, a, "O6", 0x0654, 0x084B, [
            r"cmp byte \[di\+0x4051\],0x0$", r"mov ax,\[di\+0x403d\]$", r"mov ax,\[di\+0x403f\]$", r"call 0xdbd:0x8f$",
            r"mov ax,\[0x47d8\]$", r"call 0x12ce:0x11c8$", RCMP, r"jc 0x75e$", r"call 0x12ce:0x11c8$",
            r"call 0x12ce:0x11bc$", r"call 0x12ce:0x11e0$", r"add ax,cx$", RCMP, r"jna 0x7bd$", r"call 0x12ce:0x11bc$",
            r"call 0x12ce:0x11e0$", r"mov cx,0x2$", r"call 0x12ce:0xa9d$", r"add ax,0xa$", RCMP, r"jna 0x7ed$",
            r"add ax,0x96$", r"mov al,0x0$", DAMAGE, r"mov dx,0xa$", POINTS]),
        R.real_at(g, a, "O6", 0x0708, 1.5), R.real_at(g, a, "O6", 0x072E, 1.8), R.real_at(g, a, "O6", 0x0780, 1.8),
        R.real_at(g, a, "O6", 0x07C6, 0.7),
        R.seq(g, a, "0DBD", 0x008F, 0x00D9, [r"call 0x12ce:0x11c2$", r"call 0x12ce:0x11c2$", r"call 0x12ce:0x11b6$",
                                             r"call 0x12ce:0x128f$"]),
        R.seq(g, a, "12CE", 0x11C2, 0x11CE, [r"mov cx,ax$", r"mov si,bx$", r"mov di,dx$", r"call 0xf75$"]))


@finding("K5", "CRI {5..10}: Panzer → Random(6·Waffe)+1 Schaden; Gelände → Äste 4/4/8/8/20/20, je Schritt Random(Nachbarn), Diagonalen nur {6,8,10}")
def k5(R, g, a):
    return R.all_of(
        R.seq(g, a, "O6", 0x084E, 0x0E4A, [
            r"mov di,0x20$", r"call 0x12ce:0xe02$", r"mov di,0x40$", r"call 0x12ce:0xe02$", r"shl ax,1$", r"shl ax,1$",
            r"add ax,si$", RND, r"inc ax$", DAMAGE, POINTS, r"mov word \[bp-0x2\],0x3e8$", r"cmp word \[bp-0x2\],0x7530$",
            r"cmp word \[bp\+0x6\],byte \+0x2$", r"mov word \[0x47d8\],0x4$", r"mov word \[0x47d8\],0x4$",
            r"mov word \[0x47d8\],0x8$", r"mov word \[0x47d8\],0x8$", r"mov word \[0x47d8\],0x14$",
            r"mov word \[0x47d8\],0x14$", r"call 0xcc8:0xbff$", r"cmp al,0x2$", r"mov \[di-0x17e0\],ax$",
            r"mov \[di-0x17de\],ax$", r"mov di,0x60$", r"call 0x12ce:0xe02$", r"push word \[bp-0x6\]$", RND, r"inc ax$",
            SOUND, imm(0x1f4), RND, r"push word \[0xe5c0\]$", r"call 0xcc8:0xbd8$", r"cmp word \[0x474c\],byte \+0x15$",
            r"mov \[0x47d8\],ax$", r"call 0x11f8:0x2f4$"]),
        set_is(R, g, a, "O6", 0x0020, [5, 6, 7, 8, 9, 10]), set_is(R, g, a, "O6", 0x0040, list(range(20, 30))),
        set_is(R, g, a, "O6", 0x0060, [6, 8, 10]))


@finding("K6", "Sonic Blaster 12: 2 × 100 Takte Farbe 19 = (Random(64),0,0), im zweiten Farbe 2 = 3 × Random(32)+32; dann Farbe 2 = (0,42,0), Erdrutsch")
def k6(R, g, a):
    return R.all_of(
        R.seq(g, a, "O6", 0x0E4A, 0x0FBA, [
            r"mov di,0x80$", r"call 0x12ce:0xe02$", r"call 0x431:0x3c3$", r"mov ax,0x13$", imm(0x40), RND,
            r"call far \[0xe8f6\]$", SOUND, imm(0x3e8), RND, r"add ax,0xbb8$", DELAY_ES, r"cmp word \[bp-0x2\],byte \+0x64$",
            r"mov ax,0x13$", imm(0x40), RND, r"call far \[0xe8f6\]$", r"mov ax,0x2$", imm(0x20), RND, r"add ax,0x20$",
            imm(0x20), RND, r"add ax,0x20$", imm(0x20), RND, r"add ax,0x20$", r"call far \[0xe8f6\]$", SOUND, imm(0x3e8),
            RND, r"add ax,0xdac$", DELAY_ES, r"call 0x431:0x40f$", r"mov ax,0x13$", r"mov ax,0x3f$",
            r"call far \[0xe8f6\]$", r"mov ax,0x2$", r"mov ax,0x2a$", r"call far \[0xe8f6\]$", r"push word \[0xdfa2\]$",
            r"call 0x431:0x496$"]),
        set_is(R, g, a, "O6", 0x0080, [12]))


@finding("K7", "Dirt 13..16: Radius 10/15/30/60, Farbe 2, je i = 0..R Kreise 0E26:11EA um (x+1, x−1, x) – Erde, kein Schaden, kein Zufall")
def k7(R, g, a):
    return R.all_of(
        R.seq(g, a, "O6", 0x0FBA, 0x117B, [
            r"mov di,0xa0$", r"call 0x12ce:0xe02$", r"mov word \[0x47d8\],0xa$", r"mov word \[0x47d8\],0xf$",
            r"mov word \[0x47d8\],0x1e$", r"mov word \[0x47d8\],0x3c$", r"mov ax,0x2$", r"call 0xe26:0x1dc2$",
            r"call 0x12ce:0x11b6$", r"call 0xe26:0x11ea$", r"call 0x12ce:0x11bc$", r"call 0xe26:0x11ea$",
            r"call 0xe26:0x11ea$"]),
        set_is(R, g, a, "O6", 0x00A0, [13, 14, 15, 16]))


@finding("K8", "Xplosive Dirt 17: m = Random(100)+300, k = Random(8)+3; Ring i, Korn j: r = Random(20i), x += Trunc(Random(r+1) − r/2), y − r + 2")
def k8(R, g, a):
    return R.all_of(
        R.seq(g, a, "O6", 0x117B, 0x14BF, [
            r"mov di,0xc0$", r"call 0x12ce:0xe02$", imm(0x64), RND, r"add ax,0x12c$", imm(0x8), RND, r"add ax,0x3$",
            r"mov ax,0x14$", r"mul word \[bp-0x2\]$", RND, r"mov \[di-0x6554\],bx$", r"call 0x12ce:0x11ce$", r"inc ax$",
            RND, r"call 0x12ce:0x11bc$", r"mov \[di-0x6556\],bx$", r"add ax,0x2$", r"mov \[di-0x6554\],bx$",
            r"mov ax,0x2$", r"call 0xe26:0x1fe4$", SOUND, imm(0xc8), RND, r"mov ax,0x64$", r"call 0x11f8:0x2c7$",
            r"mov ax,\[0xe8a4\]$", r"shl ax,1$", RREAL, r"call 0x12ce:0x11c8$", RCMP, r"jnc 0x14bf$", r"call 0x431:0x496$"]),
        R.real_at(g, a, "O6", 0x127F, 2.0), R.real_at(g, a, "O6", 0x1493, 100.0),
        set_is(R, g, a, "O6", 0x00C0, [17]))


@finding("K9", "Erdrutsch 0431:0496: Bild rückwärts nach Farbe 2 absuchen, Pixel darunter 0 oder 30..99 → eine Zeile tiefer; Liste bis alle stehen")
def k9(R, g, a):
    return R.seq(g, a, "0431", 0x0496, 0x056D, [
        r"mov cx,0xa$", r"rep stosw$", r"mov ax,0xa000$", r"mov di,0xfa00$", r"std$", r"mov ax,0x2$", r"repne scasb$",
        r"cmp byte \[es:di\+0x141\],0x0$", r"cmp byte \[es:di\+0x141\],0x1e$", r"cmp byte \[es:di\+0x141\],0x63$",
        r"mov ax,0x140$", r"mov al,0x2$", r"mov \[es:di\+0x140\],al$", r"mov al,\[di\]$", r"mov \[es:di\],al$",
        r"mov word \[si\],0xffff$", r"repe scasw$", r"mov al,\[es:di\+0x140\]$", r"cmp al,0x0$", r"cmp al,0x1e$",
        r"cmp al,0x63$", r"mov word \[si\],0x0$", r"mov \[es:di\+0x140\],al$", r"add di,0x140$", r"retf 0x8$"])


@finding("K10", "Fallen O7:0020: steht, wenn ein Pixel (x−3..x+3, y+1) oder beide (x±4, y+1) ∈ {2,13}; je Zeile Random(3)+1 Schaden an Inertia (k=1), Delay(FS)")
def k10(R, g, a):
    return R.all_of(
        R.seq(g, a, "O7", 0x0020, 0x0344, [
            r"call 0x364$", r"mov al,\[di\+0x4051\]$",
            r"mov word \[0x4742\],0xfffd$", r"call 0xcc8:0xbff$", r"call 0x12ce:0xe02$", r"cmp word \[0x4742\],byte \+0x3$",
            r"add ax,0x4$", r"call 0xcc8:0xbff$", r"sub ax,0x4$", r"call 0xcc8:0xbff$", r"call 0x12ce:0x12ee$",
            r"call 0x12ce:0x1301$", r"call 0xcc8:0xac0$", r"add ax,0x13$", r"call 0x431:0x5ed$", r"call 0xb20:0x2a$",
            r"inc word \[di\+0x403f\]$", r"add ax,0x4$", r"dec word \[di\+0x403d\]$", r"sub ax,0x4$",
            r"inc word \[di\+0x403d\]$", r"mov word \[0x4742\],0xfffc$", r"mov byte \[bp-0x2\],0x0$",
            r"cmp word \[0x4742\],byte \+0x4$", imm(0x3), RND, r"inc ax$", r"mov al,0x1$", DAMAGE,
            r"push word \[0xe8c8\]$", r"call 0x11f8:0x29c$", r"call 0x431:0x685$", r"call 0x431:0x7ba$"]),
        R.real_at(g, a, "O7", 0x0156, 8.0), set_is(R, g, a, "O7", 0x0000, [2, 13]))


@finding("K11", "Nach dem Schuss O4:44A7: alle Panzer neu, Crumble > 0 → O7:03C1, Fallen an → O7:0020; Schussprotokoll DS:4A80 + 7DAh·i")
def k11(R, g, a):
    return R.all_of(
        R.seq(g, a, "O4", 0x49CB, 0x4B57, [
            r"call 0x431:0x7ba$", r"cmp word \[0xe8a4\],byte \+0x0$", r"jng 0x49ff$", r"call 0xb16:0x2a$",
            r"cmp byte \[0xe8b8\],0x0$", r"call 0xb16:0x20$", r"mov ax,\[0x4796\]$", RCMP, r"mov word \[di\+0x4a86\],0x7fff$",
            r"mov \[di\+0x4a88\],bx$", r"mov \[di\+0x4a84\],bx$", r"mov \[di\+0x4a82\],bx$", r"mov \[di\+0x4a80\],bx$",
            r"call 0x0$"]),
        R.seq(g, a, "O7", 0x0364, 0x03C1, [r"call 0xcc8:0x31$", r"call 0xcc8:0x31$", r"mov al,\[0x47ec\]$",
                                           r"call 0x12ce:0xe02$", r"mov ax,0xbd$", r"mov ax,0x135$", r"mov ax,0xbd$",
                                           r"mov al,0x2$", r"call 0xcc8:0x9e4$"]),
        set_is(R, g, a, "O7", 0x0344, [1, 2, 3]))


@finding("K12", "Tod: Energie ≤ 0 → +23h = 0, Name, Random(7) → O5[0,1,2,1,3,4,5]; Schütze +1500 und +6Eh+1 (anderes Team) bzw. −1000; Erdrutsch, Fallen")
def k12(R, g, a):
    return R.seq(g, a, "O4", 0x4B57, 0x4D67, [
        r"mov byte \[0x480f\],0x0$", r"cmp word \[di\+0x404f\],byte \+0x0$", r"cmp byte \[di\+0x4051\],0x0$",
        r"mov byte \[0x480f\],0x1$", r"mov byte \[di\+0x4051\],0x0$", r"call 0x431:0x7ba$", r"call 0xe26:0x1f12$",
        imm(0x7), RND, r"call 0xb0f:0x20$", r"call 0xb0f:0x25$", r"call 0xb0f:0x2a$", r"call 0xb0f:0x25$",
        r"call 0xb0f:0x2f$", r"call 0xb0f:0x34$", r"call 0xb0f:0x39$", r"mov ax,\[di\+0x4094\]$", r"cmp ax,cx$",
        r"add ax,0x5dc$", r"mov ax,\[di\+0x409c\]$", r"inc ax$", r"sub ax,0x3e8$", r"call 0xb16:0x25$",
        r"call 0xb16:0x2a$", r"cmp byte \[0xe8b8\],0x0$", r"call 0xb16:0x20$"])


@finding("K13", "Blitz: ohne Tod und Durchgang > 10 und > Random(100) → O6:14C5(0); Zickzack Random(300)+10 oben/unten, Random(7)−3, Random(8)+1 Blitze")
def k13(R, g, a):
    return R.all_of(
        R.seq(g, a, "O4", 0x4D67, 0x4DAE, [r"cmp byte \[0x480f\],0x0$", r"cmp word \[0x47ea\],byte \+0xa$", imm(0x64),
                                           RND, r"mov ax,\[0x47ea\]$", r"call 0xb13:0x25$", r"mov byte \[0x480f\],0x1$",
                                           r"jmp 0x4b57$"]),
        R.seq(g, a, "O6", 0x14C5, 0x18CC, [
            r"mov ax,0xc8$", r"call far \[0xe8f6\]$", r"mov ax,0xc9$", r"mov ax,0x32$", r"call far \[0xe8f6\]$",
            r"mov ax,0x12c$", RND, r"add ax,0xa$", r"mov ax,0x12c$", RND, r"add ax,0xa$", r"call 0x12ce:0x11ce$",
            r"mov word \[bp-0x2\],0x1f$", imm(0x7), RND, r"sub ax,0x3$", r"sub ax,0xbd$", r"call 0x12ce:0x11ce$",
            r"call 0x12ce:0x11c8$", r"cmp word \[bp-0x2\],0xbd$", imm(0x8), RND, r"inc ax$", r"call 0xcc8:0xbff$",
            r"cmp word \[0x47de\],byte \+0x14$", r"cmp word \[0x47de\],byte \+0x1d$", r"mov ax,0x3$", DAMAGE,
            r"mov al,0xc8$", r"call 0xcc8:0xbb6$", r"mov al,0xc9$", r"call 0xcc8:0xbb6$", r"mov al,0xc9$",
            r"call 0xcc8:0xbb6$", imm(0x7), RND, r"call 0xcc8:0xbd8$", SOUND, imm(0x258), RND, r"add ax,0x258$",
            r"mov cx,0x3$", r"idiv cx$", r"mov \[0x4052\],ax$", r"mov word \[0x4052\],0xb$", r"call 0xe0$",
            r"call 0x431:0x7ba$", r"cmp word \[0xe8a4\],byte \+0x0$", r"call 0xb16:0x2a$", r"call 0xb16:0x20$"]),
        R.real_at(g, a, "O6", 0x1543, 189.0))


@finding("K14", "Rundenende: alle Lebenden im selben Team (+66h) → DS:47E0 = Team, 0431:0000, je Satz +68h += 100 − Energie (außerhalb 0..100: 100)")
def k14(R, g, a):
    return R.seq(g, a, "O4", 0x4DAE, 0x4EF3, [
        r"mov byte \[0x4813\],0x1$", r"cmp byte \[di\+0x4051\],0x0$", r"mov ax,\[di\+0x4094\]$", r"mov \[0x4744\],ax$",
        r"cmp byte \[di\+0x4051\],0x0$", r"mov ax,\[di\+0x4094\]$", r"mov byte \[0x4813\],0x0$", r"mov \[0x47e0\],ax$",
        r"mov ax,\[0x4744\]$", r"mov \[0x47e0\],ax$", r"jmp 0x4770$", r"call 0x431:0x0$", r"mov ax,0x64$", r"sub ax,dx$",
        r"cmp word \[di\+0x404f\],byte \+0x64$", r"mov word \[0x4742\],0x64$", r"mov ax,\[di\+0x4096\]$",
        r"mov \[di\+0x4096\],cx$", r"call 0xc48:0x269$"])


@finding("K15", "Todesexplosion O5:29A0: Satz → Satz 0, Random(10) → k = 1 (0–1), 2 (2–4), 3 (5–7), 4 (8–9); Waffen 1..k nacheinander über O6:00E0")
def k15(R, g, a):
    return R.seq(g, a, "O5", 0x29A0, 0x2B40, [
        imm(0x2), RND, r"call 0x431:0x7ba$", r"call 0xb20:0x25$", r"cmp word \[bp-0xe\],byte \+0x64$",
        r"call 0xb20:0x25$", r"mov di,0x402e$", r"mov ax,0x74$", r"call 0x12ce:0xa81$", imm(0xa), RND,
        r"mov word \[0x474c\],0x1$", r"mov word \[0x474c\],0x2$", r"mov word \[0x474c\],0x3$", r"mov word \[0x474c\],0x4$",
        r"mov \[0x4052\],ax$", r"mov ax,\[0x403d\]$", r"mov ax,\[0x403f\]$", r"call 0xb13:0x20$"])


@finding("K16", "Schaden der Todesexplosionen: O5:001F Random(10)+5, O5:1BDE Random(10)+10, O5:09EC/0C6C Random(3)+1 je getroffenem Panzerpixel")
def k16(R, g, a):
    hit = [r"cmp word \[0x474c\],byte \+0x14$", r"cmp word \[0x474c\],byte \+0x1d$"]
    return R.all_of(
        R.seq(g, a, "O5", 0x039D, 0x03EE, [r"call 0xe26:0x1fcc$"] + hit + [imm(0xa), RND, r"add ax,0x5$", DAMAGE]),
        R.seq(g, a, "O5", 0x2056, 0x20AB, [r"call 0xe26:0x1fcc$"] + hit + [imm(0xa), RND, r"add ax,0xa$", DAMAGE]),
        R.seq(g, a, "O5", 0x0A49, 0x0A85, [r"call 0xcc8:0xbff$", imm(0x3), RND, r"inc ax$", DAMAGE]),
        R.seq(g, a, "O5", 0x0B3A, 0x0B76, [r"call 0xcc8:0xbff$", imm(0x3), RND, r"inc ax$", DAMAGE]),
        R.seq(g, a, "O5", 0x0CEC, 0x0D2C, [r"call 0xcc8:0xbff$", imm(0x3), RND, r"inc ax$", DAMAGE]),
        R.seq(g, a, "O5", 0x0D2C, 0x0D40, [imm(0xe), RND]))
