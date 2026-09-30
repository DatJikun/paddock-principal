PF.library(String.raw`
# ============================================================
#  PixelFaces · biblioteka 24×24
#  Część:  == slot/nazwa  [@x,y]  [mirror]  [flip]  [tagi]
#    @x,y     lewy górny róg siatki na płótnie (domyślnie 0,0)
#    mirror   rysujesz lewą połowę, prawa powstaje z odbicia
#    flip     część asymetryczna; osoba losuje stronę (np. przedziałek)
#    era:1950-1969  lata, w których część jest w modzie
#    w:3      waga losowania (domyślnie 1)
#    role:driver|staff|mechanic   kto może to nosić
#    age:40   od jakiego wieku (zmarszczki)
#    tex:straight|wavy|curly      struktura włosów
#    len:short|long               długość (skłonność osoby)
#  Pusta linia kończy siatkę. Znaki = role kolorów (PF.ROLES):
#    .  pusto         K  kontur        _  maska łysienia
#    l S s k  skóra: światło, baza, cień, kontur
#    j H h d  włosy: światło, baza, cień, kontur    b brwi   u zarost
#    W E e  oko: białko, źrenica, tęczówka          m M  usta
#    T t  barwa 1 zespołu   R r  barwa 2   w x  biel   C c  strój   n koszula
#    G g L  okulary: oprawka, szkło, odblask
# ============================================================
size 24
crop head 4 1 16 16

# ---------- szyja i głowa ----------
== neck mirror @7,15
kssss
ksSSS
kSSSS
kSSSS
kSSSS

== head/oval mirror @4,2
....kkkk
..kkSSSS
.ksSSSSS
.ksSSlSS
.ksSSSSS
.ksSSSSS
.ksSSSSS
.ksSSSSS
.ksSSSSS
.ksSSSSS
.kssSSSS
..kssSSS
..kksSSS
...kksSS
....kksS
......kk

== head/square mirror @4,2 w:.8
....kkkk
..kkSSSS
.ksSSSSS
.ksSSlSS
.ksSSSSS
.ksSSSSS
.ksSSSSS
.ksSSSSS
.ksSSSSS
.ksSSSSS
.kssSSSS
.kssSSSS
.kksSSSS
..kksSSS
...kkssS
.....kkk

== head/narrow mirror @4,2 w:.8
....kkkk
..kkSSSS
.ksSSSSS
.ksSSlSS
.ksSSSSS
.ksSSSSS
.ksSSSSS
.ksSSSSS
..ksSSSS
..ksSSSS
..kssSSS
..kssSSS
...kssSS
...kksSS
.....kkS
......kk

== head/round mirror @4,2 w:.8
....kkkk
..kkSSSS
.ksSSSSS
.ksSSlSS
.ksSSSSS
.ksSSSSS
.ksSSSSS
.ksSSSSS
.ksSSSSS
.ksSSSSS
.ksSSSSS
.kssSSSS
..kssSSS
...kksSS
.....kkS
.......k

== head/heart mirror @4,2 w:.6
....kkkk
..kkSSSS
.ksSSSSS
.ksSSlSS
.ksSSSSS
.ksSSSSS
.ksSSSSS
.ksSSSSS
..ksSSSS
..ksSSSS
..kssSSS
...ksSSS
...kksSS
....kkSS
.....kkS
......kk

== ears/flat mirror @3,9
.k
kS
ks
.k

== ears/big mirror @2,8 w:.5
..k
.kS
.kS
.ks
..k

# ---------- twarz ----------
== eyes/plain mirror @7,9
kkk
WeW

== eyes/heavy mirror @7,9
ssk
WeW

== eyes/narrow mirror @7,9 w:.7
...
kEk

== eyes/round mirror @7,9 w:.7
.k.
WeW
.s.

== brows/straight mirror @7,8
bbb

== brows/arched mirror @7,7
.bb
b..

== brows/thick mirror @7,7 w:.7
.b.
bbb

== brows/low mirror @7,8 w:.7
bbb.

== nose/button mirror @10,11
.l
ss

== nose/long mirror @10,10
.l
.l
sk

== nose/wide mirror @9,11 w:.8
..l
sks

== mouth/line mirror @9,14
mmm

== mouth/smile mirror @9,13
m..
.mm

== mouth/lips mirror @9,14
.mm
.MM

== mouth/firm mirror @10,14 w:.7
mm

# ---------- wiek: nakładki od danego wieku ----------
== age/1 mirror @6,11 age:34
......
...s..
...s..

== age/2 mirror @5,5 age:46
....sss
.......
.......
.......
s......
.......
...ss..

== age/3 mirror @5,10 age:58
s......
.......
...s...
.......
..s....
.s.....

# ---------- zarost ----------
== beard/none w:6

== beard/stubble mirror @6,12
u.....
uu..uu
uu....
.uuuuu
....uu

== beard/moustache mirror @8,13 era:1950-2100
dhhH

== beard/walrus mirror @8,13 era:1968-1990 w:1.5
dhhH
d...

== beard/sideburns mirror @5,6 era:1966-1982 w:2
dh
dh
dh
dh
dh
dh
.h

== beard/full mirror @5,6 era:1965-2100
dh.....
dh.....
dh.....
dh.....
dh.....
dh.....
dhh....
.dhhhhh
.dhH...
..dhHHh
...dhHH
.....dh

== beard/goatee mirror @8,13 era:1990-2100
.dhH
....
..dH
..dh

# ---------- włosy ----------
== hair/crew mirror @4,1 era:1950-1975 w:3 len:short
....dddd
..ddhHHH
.dhHHjjH
.dhHHHHH
.dhhhhhh
.dh.....
.dh.....
.d......

== hair/slick mirror @4,0 era:1950-1968 w:3
.....ddd
...ddHHH
..dHHjjj
.dhHHHHH
.dhHhhhh
.dhh....
.dh.....
.dh.....
.d......

== hair/sidepart flip @4,0 w:2
.....dddddd.....
...ddHHHHHHdd...
..dHHjjjjHHhHd..
.dhHHHHHHHHdhHd.
.dhhhhhhhHdSdhHd
.dh.......S..hd.
.dh...........d.
.d............d.

== hair/mop mirror @3,0 era:1962-1978 w:2
......ddd
....ddHHH
...dHHjjj
..dhHHHHH
..dhHHHHH
..dhHHhHH
..dhhdhhh
..dhh....
..dh.....
..dh.....

== hair/long mirror @2,0 era:1967-1984 w:2 len:long
......dddd
....ddHHHd
...dHHjjHd
..dHHjjHd.
..dHHHHd..
.dhHHHd...
.dhHHh....
.dhHHh....
.dhHHh....
.dhHH.....
.dhHH.....
.dhHh.....
.dhHh.....
.dhhh.....
..dhh.....

== hairback/long mirror @2,13
dhhH....
dhHHh...
dhHHh...
.dhHh...
.dhhh...
..ddd...

== hair/shag mirror @3,0 era:1970-1985 tex:wavy|curly w:2
......ddd
...dddHHH
..dHHjjHH
..dHHHHHH
.dhHHhHHh
.dhHhdhhd
.dhh.d...
.dhh.....
.dhh.....
.dh......
..d......

== hair/afro mirror @1,0 era:1968-1985 tex:curly w:3
......ddddd
....ddHHjjH
...dhHHjjHH
..dhHHHHHHH
.dhHHHHHHHH
.dhHHHHhhhh
.dhHHh.....
.dhHHh.....
..dhHh.....
..ddh......

== hair/curly mirror @4,0 tex:curly w:2
.....ddd
...ddHjH
..dHjHHj
.dHHHjHH
.dhHhHhH
.dhh.d.d
.dh.....
.d......

== hair/feathered flip @3,0 era:1976-1992 w:2
.......dddddd.......
.....ddHHHHHHdd.....
....dHjjjjHHHHHd....
...dhHHHHHHHHHHHd...
..dhHHHHHHHHHhhHhd..
..dhHhhhhhhHhd.dhhd.
..dhhd.......d..dhd.
..dhh............hd.
..dhh............hd.
...dh............d..

== hair/mullet mirror @3,0 era:1982-1994 w:2
......ddd
....ddHHH
...dHHjjH
..dhHHHHH
..dhhhhhh
..dhh....
..dhh....
..dhh....
..dh.....

== hairback/mullet mirror @2,9
dh......
dhh.....
dhhh....
dhhh....
.dh.....

== hair/crop mirror @4,1 era:1990-2100 w:3 len:short
....dddd
..ddHjHH
.dHHjHjH
.dhHHHHH
.dhhHhHh
.dh.....
.d......

== hair/buzz mirror @4,1 era:1985-2100 w:1.5 len:short
....hhhh
..hhhhhh
.hhhhhhh
.hhhhhhh
.h......

== hair/fade flip @4,0 era:2005-2100 w:2
.....dddddd.....
...ddHHjjjHHdd..
..dHHHHjjHHHHHd.
.dhHHHHHHHHHhhd.
.hhHhhhHhhhhhhh.
.hh............h
.u..............

# ---------- łysienie: maski ścierają włosy (_) ----------
== recede/1 mirror @6,4
.__...
___...

== recede/2 mirror @5,3
.______
.______
.______

== recede/3 mirror @4,0
________
________
________
________
..______
..______

# ---------- stroje ----------
== body/shirt50 mirror @0,17 role:driver era:1950-1961
............
......KKw...
....KKwwxw..
..KKwwwwxKw.
.Kwwwwwwwxw.
Kwwwwwwwwxwx
Kwwwwwwwwxwx

== body/overall60 mirror @0,17 role:driver era:1960-1970
............
.......Kxx..
....KKKwwxx.
..KKwwwwwxxx
.Kwwwwwwwwwx
KwwwwwwwwwKw
KwwwwwwwwwKw

== body/overall70 mirror @0,17 role:driver era:1970-1987
............
.......KRR..
....KKKRRRR.
..KKTTTKRRRR
.KTTTTTTKKKK
KTTTTTTwwTTT
KTTTTTTTTTTT

== body/suit90 mirror @0,17 role:driver era:1986-2100
............
.......KTT..
....KKKTTTT.
..KKRRRKTTTT
.KRRRRTTKKKK
KTTTTTTwwTTT
KTTTTTTTTTTT

== body/suit mirror @0,17 role:staff
............
.......Knn..
....KKKCnnn.
..KKCCCCKnR.
.KCCCCCCCKRR
KCCCCCCCCKnR
KCCCCCCCCKnR

== body/sweater mirror @0,17 role:staff era:1955-1990 w:.7
............
.......Knn..
....KKKCnnn.
..KKCCCCCnR.
.KCCCCCCCCKR
KCCCCCCCCCCK
KCCCCCCCCCCC

== body/teamshirt mirror @0,17 role:mechanic|staff era:1968-2100 w:.6
............
.......KRR..
....KKKTRRT.
..KKTTTTTTKT
.KTTTTTTTTTK
KTTTTTTTTTTT
KTTTTTTTTTTT

== body/overallmech mirror @0,17 role:mechanic
............
.......KTT..
....KKKTTTT.
..KKTTTTTTKT
.KTTTTTTTRKR
KTTTTTTTTRKR
KTTTTTTTTRKR

# ---------- okulary ----------
== glasses/round mirror @6,8 era:1950-1972
.GGG.
G...G
GgLgG
.GGGG

== glasses/aviator mirror @5,8 era:1970-1990
GGGGGG
Gg.g.G
GgggLG
.GgggG
..GGG.

== glasses/square mirror @5,8 era:1985-2100
GGGGGG
G....G
GggLgG
GGGGGG

== glasses/browline mirror @6,8 era:1950-1975
GGGGGG
G....G
.G..G.
..GG..
`);
