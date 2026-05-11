# Natural Selection Simulator

Ez a projekt a szakdolgozatomhoz készült természetes szelekciót modellező szimulátor.  
A célom az volt, hogy Unity-ben készítsek egy olyan egyszerű, de bővíthető szimulációs környezetet, ahol különböző tulajdonságokkal rendelkező élőlények mozognak, táplálkoznak, szaporodnak, ragadozókkal találkoznak, és ezek alapján hosszabb távon megfigyelhető, hogyan változik a populáció összetétele.

A projekt eredetileg klasszikus Unity `MonoBehaviour` alapú megoldásként indult, később viszont egy nagyobb architekturális átdolgozáson ment keresztül. Ennek részeként a szimuláció egyes részei adat-orientáltabb irányba lettek átszervezve, és bekerült egy részleges DOTS/ECS alapú mirror réteg is.

## A projekt célja

A szimuláció nem biológiailag teljesen pontos modell akar lenni, hanem egy programozási és szimulációs kísérlet.  
A fő kérdés az volt, hogy hogyan lehet játékfejlesztési eszközökkel olyan rendszert készíteni, ahol az egyedek nem előre megírt fix viselkedést követnek, hanem az aktuális állapotuk alapján döntenek.

A szimulációban az élőlények többek között az alábbi tényezők alapján működnek:

- energia
- életkor
- mozgási sebesség
- érzékelési távolság
- szaporodási képesség
- ragadozó vagy növényevő viselkedés
- vadászat vagy menekülés esélye
- döntéshozatal Utility AI alapján

A projektben megjelenik egy egyszerűsített Hawks and Doves jellegű viselkedési minta is. Ennek lényege, hogy az egyedek nem teljesen azonos módon reagálnak a környezetükre és más élőlények jelenlétére. Egyes egyedek óvatosabb, elkerülőbb stratégiát képviselnek, míg mások kockázatvállalóbb, dominánsabb viselkedést mutathatnak.

Ezt nem teljes játékelméleti modellként kezeltem, hanem a szimuláció viselkedési változatosságának részeként. A cél az volt, hogy az egyedek között ne csak fizikai tulajdonságokban, például sebességben vagy érzékelési távolságban legyen különbség, hanem viselkedési stratégia szintjén is.

## Főbb funkciók

A projektben jelenleg növényevők, ragadozók és táplálékobjektumok szerepelnek.  
A növényevők táplálékot keresnek, energiát fogyasztanak, szaporodhatnak, illetve menekülhetnek a ragadozók elől.  
A ragadozók prédát keresnek, vadásznak, és sikeres vadászat esetén energiát nyernek.

A szimulációban szereplő egyedeknek van saját életciklusa. Az energiafogyás, az öregedés, a szaporodás és az elpusztulás mind része a modellnek. A program statisztikákat is gyűjt a populáció alakulásáról, például az egyedszámról, energiáról, halálozásról, ragadozásról és szaporodási eseményekről.

A projektben jelenleg növényevők, ragadozók és táplálékobjektumok szerepelnek.  
A növényevők táplálékot keresnek, energiát fogyasztanak, szaporodhatnak, illetve menekülhetnek a ragadozók elől. Emellett bizonyos viselkedési különbségek is megjelennek közöttük, például óvatosabb vagy kockázatvállalóbb stratégia formájában, ami a Hawks and Doves modell egyszerűsített értelmezéséhez kapcsolódik.  
A ragadozók prédát keresnek, vadásznak, és sikeres vadászat esetén energiát nyernek.
## Döntéshozatal

Az egyedek viselkedésének egyik fontos része a Utility AI alapú döntési rendszer.  
Ahelyett, hogy minden helyzetre fix `if-else` logika döntene, az egyes lehetséges akciók pontszámot kapnak, és az élőlény az aktuálisan legkedvezőbbnek tűnő akciót választja.

Ilyen akció lehet például:

- táplálék keresése
- pár keresése
- vándorlás
- vadászat
- aktuális állapot fenntartása

Ez azért volt fontos része a projektnek, mert így a viselkedés később könnyebben bővíthető. Új döntési szempont vagy új akció hozzáadásakor nem kell az egész állapotgépet teljesen átírni.
A Utility AI döntési rendszer mellett a szimulációban megjelenik egy egyszerűsített Hawks and Doves jellegű viselkedési minta is. Ez a gyakorlatban azt jelenti, hogy az egyedek nem minden helyzetben azonos módon viselkednek: lehetnek óvatosabb, konfliktuskerülőbb egyedek, illetve olyanok is, amelyek nagyobb kockázatot vállalnak.

Ez a megoldás azért hasznos, mert a populáció viselkedése így változatosabbá válik. A szimulációban nem csak az számít, hogy egy egyed gyorsabb, erősebb vagy jobb érzékeléssel rendelkezik, hanem az is, hogy milyen stratégia szerint reagál bizonyos helyzetekre.
## Architektúra

A projektben az élőlények működése több külön részre van bontva.  
Nem egyetlen nagy script kezeli az összes viselkedést, hanem külön komponensek felelnek az egyes feladatokért.

Például:

- `EnergyManager` kezeli az energiafogyasztást és energianyerést
- `MovementManager` kezeli a mozgással kapcsolatos logikát
- `AgeManager` kezeli az életkort és öregedést
- `ObservationManager` kezeli a környezet érzékelését
- `ReproductionManager` kezeli a szaporodást
- `EatingManager` kezeli a táplálkozást
- `CreatureUtilityBrain` kezeli a Utility AI döntéseket

A projektben később megjelent egy ECS mirror réteg is. Ennek célja az volt, hogy a klasszikus GameObject alapú működés mellett bizonyos adatokat ECS komponensekben is lehessen kezelni. Ez nem teljes ECS átírás, inkább egy hibrid megoldás, amely lehetőséget ad az adat-orientáltabb működés kipróbálására.

## DOTS / ECS átdolgozás

A projekt egyik nagyobb változása az volt, hogy a korábbi klasszikus Unity-s felépítés mellé bekerült egy DOTS/ECS irányú átdolgozás.

Ennek oka főleg az volt, hogy egy szimulációban sok hasonló objektum működik egyszerre. Ilyen esetben a hagyományos `MonoBehaviour` alapú megoldás gyorsan nehezen átláthatóvá és kevésbé hatékonnyá válhat. Az ECS ezzel szemben jobban illeszkedik az olyan problémákhoz, ahol sok egyed hasonló adatokkal és viselkedéssel rendelkezik.

A jelenlegi megoldás hibrid jellegű. A Unity GameObject alapú objektumok továbbra is megmaradtak, de egyes állapotok és döntési adatok ECS komponensekben is tükröződnek. Ezt az `ECSMirrorBridge` kezeli.

## Statisztika és diagnosztika

A szimuláció futása közben a program statisztikai adatokat is gyűjt.  
Ezek segítenek megfigyelni, hogyan változik a populáció a szimuláció során.

A gyűjtött adatok között szerepel például:

- növényevők száma
- ragadozók száma
- táplálékobjektumok száma
- átlagos energia
- szaporodási események száma
- utódok száma
- ragadozási próbálkozások száma
- sikeres vadászatok száma
- elpusztulási okok
- túlélési arány

Ezek az adatok a szakdolgozatban is felhasználhatók a szimuláció működésének bemutatására és kiértékelésére.

## Használt technológiák

A projekt Unity-ben készült, C# nyelven.

Használt főbb technológiák:

- Unity
- C#
- MonoBehaviour alapú komponensrendszer
- Unity DOTS / ECS
- Unity Jobs irányú optimalizációs megoldások
- Utility AI döntéshozatal

## Projekt megnyitása
A fejlesztéshez használt Unity verzió:

```text
Unity 2022.3.62f3