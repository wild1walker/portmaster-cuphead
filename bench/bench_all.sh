#!/bin/bash
# Device-side: plays every level group unattended with the port's test hooks (converter/helper
# TestHooks: playlist, godmode, Chaser, stalled-phase fallback, unstick, bench log) and the host's
# scripted pad, one game session per group, logs in bench/<group>.log (frame stats: UA_FPS lines).
# Usage (beside ua_host, run.sh): bench_all.sh [group ...]   groups: bosses1 bosses2 dice dlc shmup run
# BENCH_TAG=<tag>: logs bench/<group>_<tag>.log (A/B runs; other env, e.g. GC_INITIAL_HEAP_SIZE, passes through)
# PHASEHOLD=<s>: coverage runs, every boss state held that long (TestHooks CUPHEAD_TEST_PHASEHOLD).
# More groups: maps (world maps), hubs (shops, Elder Kettle's house, kitchen, chess castle), tutorials,
# cutscenes, expert (the heaviest fights on Expert), chalice (one of each kind as Ms. Chalice).
# Summarize on the Mac: host/benchsum.py bench/*.log
cd "$(dirname "$0")" || exit 1
mkdir -p bench
MAXSECS=${MAXSECS:-330}
MENU='30:a 36:a 50:a 56:a 62:a'                     # title, main menu, save slot (the hook starts the level)
BOSS_IN='75@10:x~9.9 80@1.5:a'                      # fire (Chaser), jump now and then
SHMUP_IN='75@10:x~9.9 80@6:up~1.2 83@6:down~1.2'    # plane: fire, drift up and down
RUN_IN='75@10:x~9.9 75@10:right~9.9 80@1.2:a'       # run-and-gun: right + fire + jumps (+ unstick)

declare -A LIST INPUT
LIST[bosses1]="Veggies Slime Frogs Flower Baroness Clown Dragon Bee"
LIST[bosses2]="Pirate Robot SallyStagePlay Mouse Train Devil"
LIST[dice]="DicePalaceBooze DicePalaceChips DicePalaceCigar DicePalaceDomino DicePalaceRabbit DicePalaceFlyingHorse DicePalaceRoulette DicePalaceEightBall DicePalaceFlyingMemory DicePalaceMain Mausoleum"
LIST[dlc]="OldMan RumRunners SnowCult Airplane Saltbaker ChessPawn ChessKnight ChessBishop ChessRook ChessQueen Graveyard"
LIST[shmup]="FlyingBlimp FlyingGenie FlyingBird FlyingMermaid FlyingCowboy"
LIST[run]="Platforming_Level_1_1 Platforming_Level_1_2 Platforming_Level_2_1 Platforming_Level_2_2 Platforming_Level_3_1 Platforming_Level_3_2"
LIST[maps]="Map1 Map2 Map3 Map4 MapDLC"
LIST[graveyard]="Graveyard"
LIST[mausoleum]="Mausoleum"
LIST[cutscenes2]="Map1 scene:scene_cutscene_kingdice Map1 scene:scene_cutscene_devil Map1 scene:scene_cutscene_outro MapDLC scene:scene_cutscene_dlc_intro MapDLC scene:scene_cutscene_dlc_saltbaker_prebattle MapDLC scene:scene_cutscene_dlc_ending"
LIST[expert2]="Saltbaker"
LIST[hubs]="scene:scene_shop scene:scene_shop_DLC scene:scene_level_house_elder_kettle scene:scene_level_kitchen scene:scene_level_chess_castle"
LIST[tutorials]="Tutorial ShmupTutorial ChaliceTutorial"
LIST[cutscenes]="scene:scene_cutscene_intro scene:scene_cutscene_world2 scene:scene_cutscene_world3 scene:scene_cutscene_kingdice scene:scene_cutscene_devil scene:scene_cutscene_outro scene:scene_cutscene_dlc_intro scene:scene_cutscene_dlc_saltbaker_prebattle scene:scene_cutscene_dlc_ending"
LIST[expert]="Devil Clown SallyStagePlay FlyingMermaid FlyingCowboy OldMan Airplane SnowCult Saltbaker"
LIST[chalice]="Veggies Platforming_Level_1_2 FlyingBlimp Saltbaker"
MAP_IN='75@16:lright~3.8 79@16:ldown~3.8 83@16:lleft~3.8 87@16:lup~3.8'   # walk a square, never press A
SCENE_IN='75@2.5:a~0.2'                                                  # turn pages, skip dialogue
TUT_IN='75@10:x~9.9 75@10:right~9.9 80@1.2:a 81@4:b~0.3'                 # run, fire, jump, dash
declare -A EXTRA SECS
INPUT[maps]=$MAP_IN; SECS[maps]=150
INPUT[hubs]=$SCENE_IN; SECS[hubs]=60
INPUT[cutscenes]=$SCENE_IN; SECS[cutscenes]=120
INPUT[cutscenes2]=$SCENE_IN; SECS[cutscenes2]=90
INPUT[tutorials]=$TUT_IN
EXTRA[expert]="CUPHEAD_TEST_MODE=hard"
EXTRA[expert2]="CUPHEAD_TEST_MODE=hard"
EXTRA[chalice]="CUPHEAD_TEST_CHARM=charm_chalice"
INPUT[shmup]=$SHMUP_IN
INPUT[run]=$RUN_IN

groups=${*:-bosses1 bosses2 dice dlc shmup run}
for g in $groups; do
    list=${LIST[$g]}
    [ -n "$list" ] || { echo "unknown group $g"; continue; }
    n=$(echo $list | wc -w)
    max=${SECS[$g]:-$MAXSECS}
    secs=$(( n * (max + 45) + 150 ))
    unstick=0; [ "$g" = run ] || [ "$g" = tutorials ] || [ "$g" = chalice ] && unstick=150
    echo "$(date +%T) group $g: $n levels, at most $secs s"
    env ${EXTRA[$g]} UA_PAD_NAME='Xbox Wireless Controller' UA_FPS=5 CUPHEAD_TEST_BENCH=1 CUPHEAD_TEST_GODMODE=1 \
    CUPHEAD_TEST_WEAPON=homing CUPHEAD_TEST_MAXSECS=$max CUPHEAD_TEST_QUIT=1 CUPHEAD_TEST_UNSTICK=$unstick \
    CUPHEAD_TEST_PHASEHOLD=${PHASEHOLD:-} \
    CUPHEAD_TEST_PLAYLIST="${list// /,}" UA_INPUT_SCRIPT="$MENU ${INPUT[$g]:-$BOSS_IN}" \
        ./run.sh "bench/$g${BENCH_TAG:+_$BENCH_TAG}.log" "$secs" & rp=$!
    sleep 4
    pid=$(pidof ua_host)
    [ -n "$pid" ] && python3 profcpu.py "$pid" 10 8 > "bench/$g${BENCH_TAG:+_$BENCH_TAG}.cpu" 2>/dev/null &
    wait $rp
    echo "$(date +%T) group $g done: $(tail -1 "bench/$g${BENCH_TAG:+_$BENCH_TAG}.log")"
done
echo "$(date +%T) all done"
