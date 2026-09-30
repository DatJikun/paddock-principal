PF.library(String.raw`
# ============================================================
#  PixelFaces · biblioteka 16×16 (format i role: nagłówek lib24.js)
#  Oś symetrii między kolumnami 7 i 8. Oczy: y6.
# ============================================================
size 16
crop head 2 0 12 12

# ---------- szyja i głowa ----------
== neck mirror @5,10
kss
kSS
kSS
kSS

== head/oval mirror @3,1
..kkk
.kSSS
kSSSS
kSSSS
kSSSS
kSSSS
kSSSS
ksSSS
ksSSS
.kksS
...kk

== head/square mirror @3,1 w:.8
..kkk
.kSSS
kSSSS
kSSSS
kSSSS
kSSSS
kSSSS
ksSSS
ksSSS
ksSSS
.kkkk

== head/narrow mirror @3,1 w:.8
..kkk
.kSSS
kSSSS
kSSSS
kSSSS
kSSSS
kSSSS
.ksSS
.ksSS
..ksS
...kk

== head/round mirror @3,1 w:.8
..kkk
.kSSS
kSSSS
kSSSS
kSSSS
kSSSS
kSSSS
ksSSS
ksSSS
.kssS
..kkk

== head/heart mirror @3,1 w:.6
..kkk
.kSSS
kSSSS
kSSSS
kSSSS
kSSSS
.kSSS
.ksSS
..ksS
..kkS
....k

== ears/flat mirror @2,6
S
s

== ears/big mirror @1,5 w:.5
.k
kS
ks

# ---------- twarz ----------
== eyes/dot mirror @5,6
e

== eyes/lid mirror @5,5 w:.8
k
e

== eyes/wide mirror @4,6 w:.6
se

== brows/straight mirror @4,5
.b

== brows/wide mirror @4,5
bb

== brows/high mirror @5,4 w:.6
b

== nose/button mirror @7,8
s

== nose/long mirror @7,7
l
s

== mouth/line mirror @6,9
mm

== mouth/smile mirror @5,8
m.
.m

== mouth/thin mirror @7,9 w:.7
m

# ---------- wiek ----------
== age/1 mirror @5,8 age:38
s

== age/2 mirror @4,7 age:50
s.

== age/3 mirror @4,4 age:60
..ss
....
....
....
....
s...

# ---------- zarost ----------
== beard/none w:6

== beard/stubble mirror @4,8
u...
uu..
.kuu

== beard/moustache mirror @6,8 era:1950-2100
dh

== beard/walrus mirror @5,8 era:1968-1990 w:1.5
dhh
d..

== beard/sideburns mirror @3,4 era:1966-1982 w:2
d
h
h
h

== beard/full mirror @3,4 era:1965-2100
d....
h....
h....
hh...
dhh.h
.dhh.
..dhh

== beard/goatee mirror @6,8 era:1990-2100
dh
..
.h

# ---------- włosy ----------
== hair/crew mirror @3,0 era:1950-1975 w:3 len:short
..ddd
.dHHH
dHjjH
dhhhh
dh...
d....

== hair/slick mirror @3,0 era:1950-1968 w:3
..ddd
.dHjj
dhHHH
dhh..
dh...
d....

== hair/sidepart flip @2,0 w:2
...dddddd...
..dHHHHHdd..
.dHjjHHdhHd.
.dhhhhhhdhd.
.dh......hd.
.d........d.

== hair/mop mirror @2,0 era:1962-1978 w:2
...ddd
..dHHH
.dHjjH
.dHHHH
.dhdhd
.dh...
.dh...
..d...

== hair/long mirror @2,0 era:1967-1984 w:2 len:long
...ddd
..dHHd
.dHjh.
.dHHd.
dhHd..
dhHh..
dhHh..
dhHh..
dhHh..
dhhh..
.dd...

== hairback/long mirror @2,9
hH.
hh.
dd.

== hair/shag mirror @2,0 era:1970-1985 tex:wavy|curly w:2
...ddd
.ddHHH
.dHjjH
dhHHhH
dhhdhd
dhh...
dh....
.d....

== hair/afro mirror @1,0 era:1968-1985 tex:curly w:3
...dddd
..dHHjH
.dHjHHH
dHHHHHH
dHHHhhh
dhHh...
.dh....
..d....

== hair/curly mirror @3,0 tex:curly w:2
..ddd
.dHjH
dHjHj
dhHhH
dh.d.
d....

== hair/feathered mirror @2,0 era:1976-1992 w:2
...ddd
..dHHH
.dHjjH
.dHHHh
dhHhh.
dhH...
dhH...
.dh...
..d...

== hair/mullet mirror @2,0 era:1982-1994 w:2
...ddd
..dHHH
.dHjjH
.dhhhh
.dh...
.dh...

== hairback/mullet mirror @1,8
dh
dh
dh
.d

== hair/crop mirror @3,0 era:1990-2100 w:3 len:short
..ddd
.dHjH
dHjHj
dhHhh
dh...

== hair/buzz mirror @3,1 era:1985-2100 w:1.5 len:short
..hhh
.hhhh
hhhhh
h....

== hair/fade flip @3,0 era:2005-2100 w:2
..dddddd..
.dHHjjHHd.
dHHHjHHHhd
hhHhhhhhhh
u........u

# ---------- łysienie ----------
== recede/1 mirror @4,3
_...

== recede/2 mirror @4,2
____
____

== recede/3 mirror @3,0
_____
_____
_____
.____

# ---------- stroje ----------
== body/shirt50 mirror @0,12 role:driver era:1950-1961
.....KKw
..KKKwxw
.Kwwwwxw
Kwwwwwxw

== body/overall60 mirror @0,12 role:driver era:1960-1970
.....Kxx
..KKKwwx
.Kwwwwww
KwwwwwKw

== body/overall70 mirror @0,12 role:driver era:1970-1987
.....KRR
..KKKTKR
.KTTTTTK
KTTTwwTT

== body/suit90 mirror @0,12 role:driver era:1986-2100
.....KTT
..KKKRKT
.KRRRTTK
KTTTwwTT

== body/suit mirror @0,12 role:staff
.....Knn
..KKKCnR
.KCCCCKR
KCCCCCKR

== body/sweater mirror @0,12 role:staff era:1955-1990 w:.7
.....Knn
..KKKCnR
.KCCCCCR
KCCCCCCC

== body/teamshirt mirror @0,12 role:mechanic|staff era:1968-2100 w:.6
.....KRR
..KKKTTT
.KTTTTTK
KTTTTTTT

== body/overallmech mirror @0,12 role:mechanic
.....KTT
..KKKTTT
.KTTTTRK
KTTTTTRK

# ---------- okulary ----------
== glasses/round mirror @4,5 era:1950-1972
.G..
GgGG
.G..

== glasses/aviator mirror @4,5 era:1970-1990
GGGG
GgG.
.G..

== glasses/square mirror @4,5 era:1985-2100
GGGG
GLG.
GGG.

== glasses/browline mirror @4,5 era:1950-1975
GGGG
G.G.
`);
