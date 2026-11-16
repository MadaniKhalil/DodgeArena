# Dodge Arena – Unity ML-Agents project (solo)

A blue agent in a walled 3D arena must **collect yellow targets** while **dodging red balls** fired at it
from the edges, and move around **grey obstacles** (balls break on obstacles, so they work as cover).

Grade-5 checklist (solo) and where each item is:

| Requirement | Where |
|---|---|
| 3D environment with simple obstacles | arena, walls, 3 randomly placed obstacles |
| YAML config file | `Assets/Training/dodger_baseline.yaml` |
| Multiple hyperparameters | both YAML files set ~15 hyperparameters; 2 runs compared (baseline vs. curiosity) |
| TensorBoard | `tensorboard --logdir results` |

---

## 0. Setup (versions differ from the PDF guide)

The PDF says Unity *2020.3 or later*. This PC has Unity **6000.6**, which needs the newer toolkit:
Unity package `com.unity.ml-agents` **4.2.0** (already installed), Python **3.10.11** and `mlagents` **1.1.0**
(already installed in `..\.venv`). The YAML format and the commands are the same as in the course.

Use this instead of `conda activate unity`:

```bat
cd "D:\UNI\UNI_Practical\Reinforcement Learning"
.venv\Scripts\activate
```

(To recreate the environment on another PC: Python 3.10, then
`pip install mlagents==1.1.0 torch==2.1.2 "setuptools<70"`.)

Open the project: Unity Hub → **Add → Add project from disk** → select the `DodgeArena` folder.

```
Assets/
  Scripts/    DodgeAgent.cs, BallSpawner.cs, Ball.cs, Tags.cs   <- game + RL code
  Training/   dodger_baseline.yaml, dodger_curiosity.yaml
  Scenes/     DodgeArena.unity    <- finished reference scene (menu: Dodge Arena > Build Scene)
  Editor/     DodgeSceneBuilder.cs <- the script that generates the reference scene
```

---

## 1. Build the arena yourself (your Unity part)

Build your own scene by hand so you know every component. If stuck, open `Scenes/DodgeArena.unity` and compare.

**File → New Scene → Basic (Built-in)**, save as `Assets/Scenes/MyArena.unity`.

1. **Tags** – Edit → Project Settings → Tags and Layers: add `Agent`, `Target`, `Ball`, `Wall`
   (already there if the reference scene was built).
2. **Materials** – `Assets/Materials` already has Floor, Wall, Agent, Target, Ball. Drag them onto objects.
3. **Arena** – Create Empty `TrainingArea` at (0,0,0). As its children:
   - `Floor`: Cube, position (0,-0.25,0), scale (20,0.5,20).
   - 4 walls, tag **Wall**:

     | Name | Position | Scale |
     |---|---|---|
     | Wall N | (0, 0.5, 10.25) | (21, 1, 0.5) |
     | Wall S | (0, 0.5, -10.25) | (21, 1, 0.5) |
     | Wall E | (10.25, 0.5, 0) | (0.5, 1, 20) |
     | Wall W | (-10.25, 0.5, 0) | (0.5, 1, 20) |
   - 3 obstacles `Obstacle 0/1/2`: Cubes, tag **Wall**, scale (3,1.5,1). The script moves them each episode.
   - `Target`: Sphere, tag **Target**, scale 0.8, Sphere Collider **Is Trigger** ticked.
4. **Ball spawner** – on `TrainingArea`: Add Component → **Ball Spawner**, Ball Material = `Ball`.
5. **Agent** – Cube `Agent`, tag **Agent**, child of `TrainingArea`, position (0,0.5,-4).
   - **Rigidbody**: Use Gravity off; Constraints: freeze Position **Y**, Rotation **X** and **Z**.
   - **Dodge Agent** (script): Max Step `2000`; drag in Target, the 3 Obstacles, Spawner (`TrainingArea`).
   - **Behavior Parameters** (added automatically): Behavior Name `Dodger`, Vector Observation Space Size `5`,
     Continuous Actions `2`.
   - **Ray Perception Sensor 3D**: Detectable Tags `Target`, `Ball`, `Wall`; Rays Per Direction `8`;
     Max Ray Degrees `180`; Sphere Cast Radius `0.35`; Ray Length `22`; Stacked Raycasts `3`.
   - **Decision Requester**: Decision Period `5`.
6. **Test by hand** – Behavior Type = **Heuristic Only**, press Play: W/S move, A/D turn.
   Set it back to **Default** afterwards.
7. **More arenas = faster training** – drag `TrainingArea` into `Assets/Prefabs` (prefab), duplicate it
   ~8 times, 30 units apart. Main Camera at (0,21,-15), rotation (55,0,0).

---

## 2. Train (2 runs) and compare in TensorBoard

Two terminals, both with the venv active:

```bat
cd "D:\UNI\UNI_Practical\Reinforcement Learning"
.venv\Scripts\activate
cd DodgeArena\Assets\Training
```

Terminal 1:
```bat
mlagents-learn dodger_baseline.yaml --run-id="baseline"
```
Press **Play** in Unity when asked. Stop with Ctrl+C any time (model is saved), continue with `--resume`.
Then a second run:
```bat
mlagents-learn dodger_curiosity.yaml --run-id="curiosity"
```

Terminal 2:
```bat
tensorboard --logdir results
```
Open http://localhost:6006 and compare `Environment/Cumulative Reward`, `Environment/Episode Length`,
`Policy/Entropy`, `Losses/*` between the two runs. Want a third run? Copy a YAML, change e.g.
`learning_rate` or `hidden_units`, use a new `--run-id`.

---

## 3. Show the trained agent + video

1. Model: `Assets/Training/results/<run-id>/Dodger.onnx`.
2. Agent → Behavior Parameters → **Model** = that `.onnx`, Behavior Type = **Inference Only**. Press Play.
3. Record the screen (Win+Alt+R) and email the video as the submission says.

---

## 4. How it works (for the theory questions)

**Rewards** (`DodgeAgent.cs`): +1 target (new one spawns), −1 hit by ball (episode ends),
−0.005 per step touching a wall, −1/MaxStep every step (hurry up).

**Observations**: 5 numbers (own velocity x/z, direction to target x/z, distance to target), plus the ray
sensor: 17 rays seeing Target/Ball/Wall, 3 frames stacked so it can tell where balls are *moving*.

**Actions**: 2 continuous: move forward/back, turn left/right.

**Hyperparameters** (see comments in the YAML): `learning_rate`, `batch_size`, `buffer_size`, `num_epoch`,
`beta` (entropy → exploration), `epsilon` (PPO clip), `lambd` (GAE), `gamma` (discount),
`time_horizon`, `hidden_units`/`num_layers`. **Curiosity** adds an intrinsic reward for states the agent
can't predict yet, to help exploration.
