# Economy model

[Download the editable Excel workbook](PrehistoricFrontiers-Economy.xlsx).

The workbook contains currency sources and sinks, an opening progression plan, gathering-rate calculations, and a brief design interpretation. The **Economy** sheet presents the results; **Inputs** contains game parameters, source links, and editable assumptions.

## Currency sources

| Currency | Starting stock | Nearby deposits | Total per side |
| --- | ---: | ---: | ---: |
| Food | 1,000 | 400 | 1,400 |
| Wood | 1,000 | 500 | 1,500 |

Workers gather from berry bushes and trees, then deliver cargo to the base. Deposits are finite and do not regenerate. Kills and elapsed time award no currency. These totals exclude resources on the enemy side.

## Currency sinks

| Item | Food | Wood | Training or building time |
| --- | ---: | ---: | ---: |
| Worker | 50 | 0 | 4 s |
| Raptor | 80 | 30 | 5 s |
| Triceratops | 140 | 70 | 9 s |
| Mammoth | 220 | 120 | 14 s |
| Barracks | 0 | 150 | 12 s |
| Clinic | 100 | 120 | 10 s |

Workers train in the main base; combat units require a completed barracks. A team shares three training slots and has a population limit of 20 living units. Training cancellation refunds the paid cost. Clinics charge a construction cost but no currency per treatment.

## Opening progression

This example starts with one worker and three combat units. Train two additional workers at match start, use the starting worker to build a barracks, then order one combat unit of each type in parallel.

| Milestone | Ready at |
| --- | ---: |
| Two additional workers | 4 s |
| Barracks | 16 s |
| First new raptor | 21 s |
| First new triceratops | 25 s |
| First new mammoth | 30 s |

The plan costs **540 food and 370 wood**, leaving **460 food and 630 wood** from the starting stock. No gathering income is needed to fund it. The resulting population is nine units, assuming no losses.

The 16-second barracks timing includes an assumed four-second trip to the construction site. These are modeled timings, not measurements from a match. Player actions are available immediately; enemy combat has a 20-second preparation delay.

## Gathering and reinforcement rates

The baseline assumes three available workers: two on food and one on wood. Each carries 20 units, gathers 5 units per second, moves at 4.3 m/s, and travels an assumed 8 m each way.

```text
Gathering time per load = 20 / 5 = 4 seconds
Delivery cycle = 4 + (2 × 8 / 4.3) = 7.72 seconds
Income per worker = 20 / 7.72 × 60 = 155.4 units per minute
```

This gives approximately **310.8 food and 155.4 wood per minute** while deposits last. The calculation excludes acceleration, congestion, combat, and time spent constructing buildings.

| Unit type | Average sustainable output interval | Ready from zero stock |
| --- | ---: | ---: |
| Raptor | 15.4 s | 20.4 s |
| Triceratops | 27.0 s | 39.9 s |
| Mammoth | 46.3 s | 60.3 s |

The output interval compares resource funding with training capacity for repeated production of one type. The zero-stock timing includes whole delivery trips and full training time. The streams are finite, so these are rates while workers can keep gathering, rather than a forecast of unlimited production.

## Design interpretation

- Food supports population growth; wood connects construction with heavier army compositions.
- Barracks provide the first development milestone. Shared training slots allow a mixed trio to complete at different times without three separate production buildings.
- Clinics trade recovery time for preserving expensive units.
- Large starting stocks accelerate the opening and reduce the importance of gathering during the first expansion.
- Finite deposits make continued replacement and army composition matter. A useful balance test is to compare smaller starting stocks with larger deposits.

The source snapshot is from November 23, 2026. Parameter links in the workbook point to the project code; yellow input cells identify assumptions that can be changed to test the model.
