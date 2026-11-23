# Project verification

The Windows build was verified on November 23, 2026, using Unity 6000.6.8f1.

## Results

- All 99 C# files compiled without C# errors or warnings: 45 gameplay and support files, 12 editor files, and 42 scenario files.
- The complete set of 50 gameplay scenarios passed, with 997 successful checks after corrections and reruns.
- The asset reference scan found no unresolved GUID references or duplicate GUIDs within `Assets`.

## Coverage

The scenarios cover combat, automatic target discovery, navigation, resource gathering, construction queues, production, enemy economy, clinic treatment, fog of war, the HUD, match results, hit reactions, and falling bodies.

The enemy preparation scenario checks that the player can attack immediately, enemy combat begins only after 20 game seconds, pausing freezes the countdown, and restarting resets the delay for the enemy alone.

During verification, scenario setup was corrected to separate overlapping navigation agents, distinguish vertical ground alignment from horizontal movement, wait for reinforcements to enter detection range before checking target changes, and preserve the test runner across scene reloads. Six affected scenarios were rerun after these corrections.

## Running a scenario

Launch the Windows build from a writable working folder with a scenario flag:

```text
PrehistoricFrontiers.exe -batchmode -enemyPreparationSmoke -logFile player.log
```

The scenario writes a `*-test.txt` report to the working folder. Exit code `0` indicates success; exit code `1` indicates a failed check. Available flags are defined in the `*SmokeTest.cs` files under `Assets/PrehistoricFrontiers/Scripts`.

## Scope

These results describe the verified project version, rather than a guarantee for every possible match or hardware configuration. Editor scripts were compiled and reviewed; art generators were not rerun to regenerate existing assets during this verification.
