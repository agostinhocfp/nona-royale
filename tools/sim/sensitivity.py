"""Isolate which lever actually moves match length."""

import random, statistics
import nona_sim as S
from nona_sim import BoardProfile, Op, Player, Stats, safe_cells, track_distance


def play(num_players, roster, board, rng, *, combat=True, setback="yard", ability_dmg=(3, 2)):
    """setback: 'yard' | 'start' | 'half' — where a neutralized operator lands."""
    players = [Player(i, [Op(n, hp, sp, i) for (n, hp, sp) in roster]) for i in range(num_players)]
    stats = Stats()

    def neutralize(op):
        op.hp = op.max_hp
        stats.neutralizes += 1
        if setback == "yard":
            op.progress = -1
        elif setback == "start":
            op.progress = 0
        elif setback == "half":
            op.progress = max(0, op.progress // 2)

    def damage(t, amt):
        t.hp -= amt
        if t.hp <= 0:
            neutralize(t)
            return True
        return False

    def move(op, cells):
        op.progress += cells
        if op.progress >= board.journey:
            op.done = True
            op.progress = board.journey
            return
        if op.progress >= board.circuit or not combat:
            return
        cell = op.cell(board)
        if cell in safe_cells(board):
            return
        for p in players:
            if p.index == op.owner:
                continue
            for e in p.ops:
                if e.on_track and e.cell(board) == cell:
                    stats.collisions += 1
                    if not damage(e, S.COLLISION_DAMAGE):
                        op.progress = max(0, op.progress - 1)
                    return

    def abilities(player):
        if not combat:
            return
        fired = True
        while fired:
            fired = False
            for op in player.ops:
                if not op.on_track or op.in_home_column(board):
                    continue
                for p in players:
                    if p.index == player.index:
                        continue
                    for e in p.ops:
                        if not e.on_track or e.in_home_column(board):
                            continue
                        d = track_distance(op, e, board)
                        if d is None or d > 3:
                            continue
                        if player.energy >= 6:
                            player.energy -= 6; damage(e, ability_dmg[0]); stats.abilities += 1; fired = True
                        elif player.energy >= 3:
                            player.energy -= 3; damage(e, ability_dmg[1]); stats.abilities += 1; fired = True
                        if fired: break
                    if fired: break
                if fired: break

    for turn in range(S.MAX_TURNS):
        player = players[turn % num_players]
        player.turns += 1
        rolls, first = 0, True
        while rolls < S.MAX_ROLLS_PER_TURN:
            d1, d2 = rng.randint(1, 6), rng.randint(1, 6)
            rolls += 1
            if first:
                player.energy = min(S.ENERGY_CAP, player.energy + (d1 + d2) // 2)
                first = False
            yard = [o for o in player.ops if o.progress == -1 and not o.done]
            if yard and 6 in (d1, d2):
                if d1 == 6 and d2 == 6:
                    for o in yard[:2]: o.progress = 0
                    move_die = 0
                else:
                    yard[0].progress = 0
                    move_die = d2 if d1 == 6 else d1
            else:
                move_die = d1 + d2
            if move_die > 0:
                cf = lambda o: int(move_die * o.speed)
                op = S.choose_move(player, players, board, cf)
                if op is not None:
                    move(op, cf(op))
            abilities(player)
            if all(o.done for o in player.ops):
                stats.turns_to_win = player.turns
                return stats
            if d1 != d2:
                break
    stats.turns_to_win = S.MAX_TURNS // num_players
    return stats


def run(label, n=10000, seed=7, **kw):
    rng = random.Random(seed)
    players = kw.pop("players", 4)
    roster = kw.pop("roster", S.ROSTER_PROPOSED)
    board = kw.pop("board", S.STANDARD)
    rs = [play(players, roster, board, rng, **kw) for _ in range(n)]
    t = [r.turns_to_win for r in rs]
    print(f"{label:34} {statistics.mean(t):6.1f} {statistics.median(t):5.0f} "
          f"{sorted(t)[int(.9*len(t))]:5.0f} {statistics.mean(r.neutralizes for r in rs):6.1f} "
          f"{statistics.mean(r.abilities for r in rs):6.1f}")


print(f"{'':34} {'mean':>6} {'med':>5} {'p90':>5} {'neut':>6} {'abil':>6}")
print("\n-- what drives length (4P, 48/6, proposed band) --")
run("pure race, no combat", combat=False)
run("combat on, setback = yard", setback="yard")
run("combat on, setback = start cell", setback="start")
run("combat on, setback = half progress", setback="half")
run("combat on, ability dmg 2/1", ability_dmg=(2, 1))

print("\n-- board length (4P, proposed band, yard setback) --")
for c, h in [(24, 3), (32, 4), (36, 5), (40, 5), (48, 6), (60, 7)]:
    run(f"circuit {c} / home {h}", board=BoardProfile(f"{c}", c, h))

print("\n-- player count (48/6, proposed band) --")
for p in (2, 3, 4):
    run(f"{p} players", players=p)

print("\n-- sprint board, speed bands --")
for lbl, r in [("flat 1.0", S.ROSTER_FLAT), ("proposed 1/1.5/1.5", S.ROSTER_PROPOSED),
               ("current 1/2/3", S.ROSTER_CURRENT)]:
    run(f"sprint 24/3, {lbl}", board=S.SPRINT, roster=r)