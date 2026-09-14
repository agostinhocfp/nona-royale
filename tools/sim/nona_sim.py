"""
Nona Royale — headless Monte Carlo harness.

Plays full matches under the rules in docs/design/COMBAT_SYSTEMS.md with
simple scripted policies, to measure pacing (turns per match), combat
throughput (abilities fired, neutralizes) and board profile behaviour.

Not a balance oracle. It answers "is this number in the right order of
magnitude" so design arguments stop being arguments.
"""

import random
import statistics
from dataclasses import dataclass, field

# ----------------------------------------------------------------------
# Config
# ----------------------------------------------------------------------

@dataclass(frozen=True)
class BoardProfile:
    name: str
    circuit: int          # outer track cells
    home_column: int      # cells per colour

    @property
    def journey(self) -> int:
        return self.circuit + self.home_column

    @property
    def start_offset(self) -> int:
        return self.circuit // 4


SPRINT   = BoardProfile("Sprint",   24, 3)
STANDARD = BoardProfile("Standard", 48, 6)
LONG     = BoardProfile("Long",     60, 7)

COLLISION_DAMAGE   = 3
ENERGY_CAP         = 12
DEPLOY_REQUIREMENT = 6
MAX_ROLLS_PER_TURN = 3
MAX_TURNS          = 400

# Alpha roster: (name, max_hp, speed_multiplier)
ROSTER_PROPOSED = [("Bouncer", 12, 1.0), ("Syla", 6, 1.5), ("Kurbyn", 6, 1.5)]
ROSTER_CURRENT  = [("Bouncer", 12, 1.0), ("Syla", 6, 2.0), ("Kurbyn", 6, 3.0)]
ROSTER_FLAT     = [("Bouncer", 12, 1.0), ("Syla", 6, 1.0), ("Kurbyn", 6, 1.0)]
ROSTER_FAST     = [("Bouncer", 12, 1.5), ("Syla", 6, 1.5), ("Kurbyn", 6, 2.0)]


# ----------------------------------------------------------------------
# State
# ----------------------------------------------------------------------

@dataclass
class Op:
    name: str
    max_hp: int
    speed: float
    owner: int
    hp: int = 0
    progress: int = -1     # -1 = yard, 0..journey-1 = on path, >= journey = HOME
    done: bool = False

    def __post_init__(self):
        self.hp = self.max_hp

    @property
    def on_track(self) -> bool:
        return self.progress >= 0 and not self.done

    def in_home_column(self, board: BoardProfile) -> bool:
        return self.on_track and self.progress >= board.circuit

    def cell(self, board: BoardProfile) -> int:
        """Absolute outer-track cell, or None if not on the outer track."""
        if not self.on_track or self.progress >= board.circuit:
            return None
        return (self.owner * board.start_offset + self.progress) % board.circuit


@dataclass
class Player:
    index: int
    ops: list
    energy: int = 0
    turns: int = 0


@dataclass
class Stats:
    turns_to_win: int = 0
    neutralizes: int = 0
    collisions: int = 0
    abilities: int = 0
    yard_turns: int = 0
    energy_burned: int = 0


# ----------------------------------------------------------------------
# Rules
# ----------------------------------------------------------------------

def track_distance(a: Op, b: Op, board: BoardProfile):
    """Steps along the outer loop, either direction. None if unreachable."""
    ca, cb = a.cell(board), b.cell(board)
    if ca is None or cb is None:
        return None
    d = abs(ca - cb)
    return min(d, board.circuit - d)


def safe_cells(board: BoardProfile) -> set:
    return {i * board.start_offset for i in range(4)}


def neutralize(op: Op, stats: Stats):
    op.hp = op.max_hp
    op.progress = -1
    stats.neutralizes += 1


def apply_damage(target: Op, amount: int, board: BoardProfile, stats: Stats) -> bool:
    """Returns True if the target was neutralized."""
    target.hp -= amount
    if target.hp <= 0:
        neutralize(target, stats)
        return True
    return False


def resolve_move(op: Op, cells: int, players, board: BoardProfile, stats: Stats):
    op.progress += cells
    if op.progress >= board.journey:
        op.done = True
        op.progress = board.journey
        return
    if op.progress >= board.circuit:
        return  # home column: no collisions

    cell = op.cell(board)
    if cell in safe_cells(board):
        return

    for p in players:
        if p.index == op.owner:
            continue
        for enemy in p.ops:
            if enemy.on_track and enemy.cell(board) == cell:
                stats.collisions += 1
                killed = apply_damage(enemy, COLLISION_DAMAGE, board, stats)
                if not killed:
                    op.progress = max(0, op.progress - 1)   # bounce back
                return


# ----------------------------------------------------------------------
# Policies
# ----------------------------------------------------------------------

def choose_move(player, players, board, cells_for):
    """
    Greedy: take a kill if one is on offer, otherwise advance the operator
    closest to home. Deliberately simple — a real player is better than this,
    which makes these turn counts a mild over-estimate.
    """
    candidates = [o for o in player.ops if o.on_track]
    if not candidates:
        return None

    for op in candidates:
        cells = cells_for(op)
        landing = op.progress + cells
        if landing >= board.circuit:
            continue
        cell = (op.owner * board.start_offset + landing) % board.circuit
        if cell in safe_cells(board):
            continue
        for p in players:
            if p.index == op.owner:
                continue
            for enemy in p.ops:
                if enemy.on_track and enemy.cell(board) == cell and enemy.hp <= COLLISION_DAMAGE:
                    return op

    return max(candidates, key=lambda o: o.progress)


def spend_energy(player, players, board, stats):
    """
    Abstracts the alpha kits: 6 energy for 3 damage at range 3, or 3 energy
    for 2 damage at range 3. Fires whenever affordable and in range.
    """
    fired = True
    while fired:
        fired = False
        for op in player.ops:
            if not op.on_track or op.in_home_column(board):
                continue
            for p in players:
                if p.index == player.index:
                    continue
                for enemy in p.ops:
                    if not enemy.on_track or enemy.in_home_column(board):
                        continue
                    d = track_distance(op, enemy, board)
                    if d is None or d > 3:
                        continue
                    if player.energy >= 6:
                        player.energy -= 6
                        apply_damage(enemy, 3, board, stats)
                        stats.abilities += 1
                        fired = True
                    elif player.energy >= 3:
                        player.energy -= 3
                        apply_damage(enemy, 2, board, stats)
                        stats.abilities += 1
                        fired = True
                    if fired:
                        break
                if fired:
                    break
            if fired:
                break


# ----------------------------------------------------------------------
# Match loop
# ----------------------------------------------------------------------

def play_match(num_players, roster, board, rng):
    players = [
        Player(i, [Op(n, hp, sp, i) for (n, hp, sp) in roster])
        for i in range(num_players)
    ]
    stats = Stats()

    for turn in range(MAX_TURNS):
        player = players[turn % num_players]
        player.turns += 1

        stats.yard_turns += sum(1 for o in player.ops if o.progress == -1 and not o.done)

        rolls = 0
        first_roll = True
        while rolls < MAX_ROLLS_PER_TURN:
            d1, d2 = rng.randint(1, 6), rng.randint(1, 6)
            rolls += 1
            total = d1 + d2

            if first_roll:
                gained = total // 2
                before = player.energy
                player.energy = min(ENERGY_CAP, player.energy + gained)
                stats.energy_burned += (before + gained) - player.energy
                first_roll = False

            deployed = False
            in_yard = [o for o in player.ops if o.progress == -1 and not o.done]
            if in_yard and DEPLOY_REQUIREMENT in (d1, d2):
                if d1 == 6 and d2 == 6:
                    for o in in_yard[:2]:
                        o.progress = 0
                    deployed = True
                    move_die = 0
                else:
                    in_yard[0].progress = 0
                    deployed = True
                    move_die = d2 if d1 == 6 else d1
            else:
                move_die = total

            if move_die > 0:
                cells_for = lambda o: int(move_die * o.speed)
                op = choose_move(player, players, board, cells_for)
                if op is not None:
                    resolve_move(op, cells_for(op), players, board, stats)

            spend_energy(player, players, board, stats)

            if all(o.done for o in player.ops):
                stats.turns_to_win = player.turns
                return stats

            if d1 != d2:
                break

    stats.turns_to_win = MAX_TURNS // num_players
    return stats


def run(num_players, roster, board, n=10000, seed=1):
    rng = random.Random(seed)
    results = [play_match(num_players, roster, board, rng) for _ in range(n)]
    turns = [r.turns_to_win for r in results]
    return {
        "turns_mean": statistics.mean(turns),
        "turns_median": statistics.median(turns),
        "turns_p10": sorted(turns)[int(0.10 * len(turns))],
        "turns_p90": sorted(turns)[int(0.90 * len(turns))],
        "neutralizes": statistics.mean(r.neutralizes for r in results),
        "collisions": statistics.mean(r.collisions for r in results),
        "abilities": statistics.mean(r.abilities for r in results),
        "energy_burned": statistics.mean(r.energy_burned for r in results),
    }


def table(title, rows):
    print(f"\n{title}")
    print(f"{'':28} {'mean':>7} {'med':>6} {'p10':>5} {'p90':>5} {'neut':>6} {'coll':>6} {'abil':>6} {'burn':>6}")
    for label, r in rows:
        print(f"{label:28} {r['turns_mean']:7.1f} {r['turns_median']:6.0f} {r['turns_p10']:5.0f} "
              f"{r['turns_p90']:5.0f} {r['neutralizes']:6.1f} {r['collisions']:6.1f} "
              f"{r['abilities']:6.1f} {r['energy_burned']:6.1f}")


if __name__ == "__main__":
    N = 10000

    table("SPEED BAND — 4 players, Standard board (48/6)", [
        ("Flat 1.0 / 1.0 / 1.0",      run(4, ROSTER_FLAT,     STANDARD, N, 11)),
        ("Proposed 1.0 / 1.5 / 1.5",  run(4, ROSTER_PROPOSED, STANDARD, N, 12)),
        ("Fast 1.5 / 1.5 / 2.0",      run(4, ROSTER_FAST,     STANDARD, N, 13)),
        ("Current 1.0 / 2.0 / 3.0",   run(4, ROSTER_CURRENT,  STANDARD, N, 14)),
    ])

    table("SPEED BAND — 2 players, Standard board (48/6)", [
        ("Flat 1.0 / 1.0 / 1.0",      run(2, ROSTER_FLAT,     STANDARD, N, 21)),
        ("Proposed 1.0 / 1.5 / 1.5",  run(2, ROSTER_PROPOSED, STANDARD, N, 22)),
        ("Current 1.0 / 2.0 / 3.0",   run(2, ROSTER_CURRENT,  STANDARD, N, 24)),
    ])

    table("BOARD PROFILES — 4 players, proposed speed band", [
        ("Sprint 24/3",   run(4, ROSTER_PROPOSED, SPRINT,   N, 31)),
        ("Standard 48/6", run(4, ROSTER_PROPOSED, STANDARD, N, 32)),
        ("Long 60/7",     run(4, ROSTER_PROPOSED, LONG,     N, 33)),
    ])