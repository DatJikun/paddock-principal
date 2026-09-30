PF.library(String.raw`
# ============================================================
#  PixelFaces · biblioteka 32×32 (format i role: nagłówek lib24.js)
#  Oś symetrii między kolumnami 15 i 16. Linia oczu: y12–13.
# ============================================================
size 32
crop head 6 1 20 20

# ---------- szyja i głowa ----------
== neck mirror @11,20
kssss
kssss
ksSSS
ksSSS
kSSSS
kSSSS
kSSSS

== head/oval mirror @7,3
....kkkkk
..kkSSSSS
.kSSSSSSS
.kSSSSSSS
kSSSSSlSS
kSSSSllSS
kSSSSSSSS
kSSSSSSSS
kSSSSSSSS
kSSSSSSSS
kSSSSSSSS
kSSSSSSSS
ksSSSSSSS
ksSSSSSSS
ksSSSSSSS
.ksSSSSSS
.kssSSSSS
..kssSSSS
..kksSSSS
....kkssS
......kkk

== head/square mirror @7,3 w:.8
....kkkkk
..kkSSSSS
.kSSSSSSS
.kSSSSSSS
kSSSSSlSS
kSSSSllSS
kSSSSSSSS
kSSSSSSSS
kSSSSSSSS
kSSSSSSSS
kSSSSSSSS
kSSSSSSSS
ksSSSSSSS
ksSSSSSSS
ksSSSSSSS
ksSSSSSSS
ksSSSSSSS
.kssSSSSS
.kksSSSSS
..kkkssSS
....kkkkk

== head/narrow mirror @7,3 w:.8
....kkkkk
..kkSSSSS
.kSSSSSSS
.kSSSSSSS
kSSSSSlSS
kSSSSllSS
kSSSSSSSS
kSSSSSSSS
kSSSSSSSS
kSSSSSSSS
kSSSSSSSS
.kSSSSSSS
.ksSSSSSS
.ksSSSSSS
.ksSSSSSS
.kssSSSSS
..ksSSSSS
..kssSSSS
...kksSSS
.....kksS
.......kk

== head/round mirror @7,3 w:.8
....kkkkk
..kkSSSSS
.kSSSSSSS
.kSSSSSSS
kSSSSSlSS
kSSSSllSS
kSSSSSSSS
kSSSSSSSS
kSSSSSSSS
kSSSSSSSS
kSSSSSSSS
kSSSSSSSS
ksSSSSSSS
ksSSSSSSS
ksSSSSSSS
ksSSSSSSS
kssSSSSSS
.kssSSSSS
..kksSSSS
....kkssS
......kkk

== head/heart mirror @7,3 w:.6
....kkkkk
..kkSSSSS
.kSSSSSSS
.kSSSSSSS
kSSSSSlSS
kSSSSllSS
kSSSSSSSS
kSSSSSSSS
kSSSSSSSS
kSSSSSSSS
.kSSSSSSS
.kSSSSSSS
.ksSSSSSS
..ksSSSSS
..ksSSSSS
..kssSSSS
...ksSSSS
...kssSSS
....kksSS
.....kkss
.......kk

== ears/flat mirror @4,12
.kk
kSk
ksS
kSs
.kk

== ears/big mirror @3,11 w:.5
.kk.
kSSk
ksS.
ksS.
kSs.
.kk.

# ---------- twarz ----------
== eyes/plain mirror @10,12
kkkk
WeeW

== eyes/soft mirror @10,12
.kkk
WeeW
.ss.

== eyes/narrow mirror @10,12 w:.7
.sss
keek

== eyes/heavy mirror @10,11
ssss
skkk
WeeW

== eyes/round mirror @10,12 w:.6
.kk.
WeeW
.WW.

== brows/straight mirror @9,11
.bbbb

== brows/arched mirror @9,10
..bbb
.b...

== brows/thick mirror @9,10 w:.7
.bbbb
.bbbb

== brows/slanted mirror @9,10 w:.7
...bb
.bb..

== brows/flat mirror @10,11 w:.6
bbbbb

== nose/button mirror @13,14
..l
..l
sks

== nose/long mirror @13,13
..l
..l
..l
..l
.ks

== nose/wide mirror @12,15 w:.8
...l
.s.l
skss

== nose/hook mirror @13,13 w:.6
.sl
.sl
..l
.kk

== mouth/line mirror @11,19
.mmmm

== mouth/lips mirror @11,19
.mmmm
..MMM

== mouth/smile mirror @11,18
m....
.mmmm

== mouth/thin mirror @12,19 w:.7
.mmm

# ---------- wiek ----------
== age/1 mirror @10,16 age:34
...s
..s.
..s.

== age/2 mirror @8,7 age:46
....ssss
........
........
........
........
s.......
s.......
..ssss..

== age/3 mirror @8,9 age:58
.....sss
........
........
........
........
........
........
........
...s....
........
..s.....
.s......
.s......

# ---------- zarost ----------
== beard/none w:6

== beard/stubble mirror @8,15
u.......
uu......
uuu..uuu
uuu.uuuu
uuu.....
.uuuuuuu
..uuuuuu
....uuuu

== beard/moustache mirror @11,18 era:1950-2100
.dhhH

== beard/walrus mirror @11,17 era:1968-1990 w:1.5
..hHH
.dhhH
dh...

== beard/sideburns mirror @7,8 era:1966-1982 w:2
dh
dh
dh
dh
dh
dh
dh
dh
dh
.h

== beard/full mirror @7,8 era:1965-2100
dh.......
dh.......
dh.......
dh.......
dh.......
dh.......
dh.......
dhh......
dhhh.....
.dhhhhhhh
.dhHHh...
..dhHHh.H
..dhHHHHH
...dhHHHH
....ddhhH
......ddd

== beard/goatee mirror @11,18 era:1990-2100
.dhhH
.....
...dH
...dH
...dh

# ---------- włosy ----------
== hair/crew mirror @6,1 era:1950-1975 w:3 len:short
......dddd
....ddHHHH
..ddHHjjHH
.dhHHjjHHH
.dhHHHHHHH
dhhHhhhhhh
dhh.......
dhh.......
dh........
dh........
d.........

== hair/slick mirror @6,1 era:1950-1968 w:3
.......ddd
.....ddHHj
...ddHHjjH
..dHHjjHHH
.dhHHHHHHH
.dhHHHhhhh
dhhhh.....
dhh.......
dh........
dh........
d.........

== hair/sidepart flip @6,1 w:2
.......ddddddd......
.....ddHHHHHHHdd....
...ddHHjjjjHHHHhd...
..dHHjjHHHHHHHdhHd..
.dhHHHHHHHHHHdhHHHd.
.dhHHhhhhhhhdhhHHHd.
dhhh..........hhHHd.
dhh............hhhd.
dh...............hd.
dh...............hd.
d.................d.

== hair/mop mirror @5,1 era:1962-1978 w:2
......ddddd
....ddHHHHH
...dHHjjHHH
..dHHjjHHHH
..dHHHHHHHH
.dhHHHHHHHH
.dhHHHHHHHH
.dhHHhHHhHH
.dhhdhhdhhd
.dhh.......
.dhh.......
.dhh.......
.dh........
..d........

== hair/long mirror @4,1 era:1967-1984 w:2 len:long
........dddd
......ddHHjd
.....dHHjjh.
....dHHjjHd.
...dhHHHHd..
..dhHHHHd...
..dhHHHd....
.dhHHHh.....
.dhHHHh.....
.dhHHHh.....
.dhHHh......
.dhHHh......
.dhHHh......
.dhHHh......
.dhHHh......
.dhHhh......
.dhhhh......
..dhhh......
...ddd......

== hairback/long mirror @4,17
dhhH......
dhHHh.....
dhHHHh....
dhHHHh....
.dhHHh....
.dhhHh....
..dhhh....
...ddd....

== hair/shag mirror @4,1 era:1970-1985 tex:wavy|curly w:2
........dddd
.....dddHHHH
....dHHjjHHj
...dHHjjHHHH
..dhHHHHHHHH
..dhHHhHHHhH
.dhHHhdhHhdh
.dhHh.d..d..
.dhHh.......
.dhHh.......
.dhh........
.dhh........
..dh........
...d........

== hair/afro mirror @4,0 era:1968-1985 tex:curly w:3
.......ddddd
.....ddHHjHH
...ddHHjjHHj
..dHHHjHHHHH
.dHHjHHHHjHH
.dHHHHHHHHHH
dHHjHHHHHHHH
dHHHHHhhhhhh
dhHHHh......
dhHHHh......
dhHHh.......
.dhHh.......
.ddh........
..d.........

== hair/curly mirror @6,1 tex:curly w:2
......dddd
....ddHjHH
...dHjHHjH
..dHHHjHHj
.dHjHHHHjH
.dhHhHhHhH
.dhh.d.d.d
.dh.......
.dh.......
..d.......

== hair/feathered mirror @4,1 era:1976-1992 w:2
........dddd
......ddHHHH
....ddHjjjHH
...dHHjjHHHH
..dHHHHHHHHh
..dHHHHHhhh.
.dhHHHhh....
.dhHHh......
.dhHHh......
.dhHHh......
.dhHH.......
.dhHh.......
.dhH........
..dh........
...d........

== hair/mullet mirror @5,1 era:1982-1994 w:2
......ddddd
....ddHHHHH
...dHHjjHHH
..dHHjjHHHH
..dhHHHHHHH
..dhhhhhhhh
.dhh.......
.dhh.......
.dhh.......
.dh........
.dh........

== hairback/mullet mirror @4,14
dh.......
dhh......
dhhh.....
dhhh.....
dhhH.....
.dhh.....
..d......

== hair/crop mirror @6,1 era:1990-2100 w:3 len:short
......dddd
....ddHjHH
..ddHjHHjH
.dHHjHHjHH
.dhHHHHHHH
.dhhHhhHhh
dhh.......
dh........
dh........
d.........

== hair/buzz mirror @6,2 era:1985-2100 w:1.5 len:short
.....hhhhh
...hhhhhhh
..hhhhhhhh
.hhhhhhhhh
.hhhhhhhhh
.hh.......
.h........
.h........

== hair/fade flip @6,1 era:2005-2100 w:2
.......ddddddd......
.....ddHHjjjHHdd....
...ddHHHHjjHHHHHd...
..dHHHHHHHHHHHHhd...
.dhHHHHHHHHHHHHhhd..
.hhHhhhHhhhhhhhhhh..
.uh...............u.
.u................u.
.u................u.

# ---------- łysienie ----------
== recede/1 mirror @8,4
........
.___....
____....

== recede/2 mirror @7,4
.________
.________
.________

== recede/3 mirror @6,0
__________
__________
__________
__________
__________
..________
..________

# ---------- stroje ----------
== body/shirt50 mirror @0,23 role:driver era:1950-1961
................
..........KKw...
.......KKKwwxw..
....KKKwwwwwxKw.
..KKwwwwwwwwwxw.
.Kwwwwwwwwwwwxwx
Kwwwwwwwwwwwwxwx
Kwwwwwwwwwwwwxwx
Kwwwwwwwwwwwwxwx

== body/overall60 mirror @0,23 role:driver era:1960-1970
................
..........Kxxx..
.......KKKwwxx..
....KKKwwwwwwxxx
..KKwwwwwwwwwwwx
.Kwwwwwwwwwwwwwx
KwwwwwwwwwwwwwKw
KwwwwwwwwwwwwwKw
KwwwwwwwwwwwwwKw

== body/overall70 mirror @0,23 role:driver era:1970-1987
................
..........KRRR..
.......KKKKRRRR.
....KKKTTTTKRRRR
..KKTTTTTTTTKKKK
.KTTTTTTTTTTTTTT
KTTTTTTTwwwTTTTT
KTTTTTTTwwwTTTTT
KTTTTTTTTTTTTTTT

== body/suit90 mirror @0,23 role:driver era:1986-2100
................
..........KTTT..
.......KKKKTTTT.
....KKKRRRRKTTTT
..KKRRRRRRTTKKKK
.KRRRRTTTTTTTTTT
KTTTTTTTwwwTTTTT
KTTTTTTTwwwTTTTT
KTTTTTTTTTTTTTTT

== body/suit mirror @0,23 role:staff
................
..........Knnn..
.......KKKCnnnn.
....KKKCCCCKnnRR
..KKCCCCCCCCKnRR
.KCCCCCCCCCCCKRR
KCCCCCCCCCCCCKnR
KCCCCCCCCCCCCKnR
KCCCCCCCCCCCCCKR

== body/sweater mirror @0,23 role:staff era:1955-1990 w:.7
................
..........Knnn..
.......KKKCnnnn.
....KKKCCCCCnnRR
..KKCCCCCCCCCnRR
.KCCCCCCCCCCCCKR
KCCCCCCCCCCCCCCK
KCCCCCCCCCCCCCCC
KCCCCCCCCCCCCCCC

== body/teamshirt mirror @0,23 role:mechanic|staff era:1968-2100 w:.6
................
..........KRRR..
.......KKKTRRRT.
....KKKTTTTTTTTT
..KKTTTTTTTTTTKT
.KTTTTTTTTTTTTTK
KTTTTTTTTTTTTTTT
KTTTTTTTTTTTTTTT
KTTTTTTTTTTTTTTT

== body/overallmech mirror @0,23 role:mechanic
................
..........KTTT..
.......KKKTTTTT.
....KKKTTTTTTTTT
..KKTTTTTTTTTRKR
.KTTTTTTTTTTTRKR
KTTTTTTTTTTTTRKR
KTTTTTTTTTTTTRKR
KTTTTTTTTTTTTRKR

# ---------- okulary ----------
== glasses/round mirror @8,11 era:1950-1972
..GGGG..
.G....GG
.GggLgG.
..GGGG..

== glasses/aviator mirror @8,11 era:1970-1990
.GGGGGGG
.Gg.ggGG
.GgggLG.
..GgggG.
...GGG..

== glasses/square mirror @8,11 era:1985-2100
.GGGGGGG
.G....G.
.GggLgG.
.GGGGGG.

== glasses/browline mirror @8,11 era:1950-1975
.GGGGGGG
.G....G.
..G..G..
...GG...
`);
