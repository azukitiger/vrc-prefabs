# VRChat Azuki Prefabs (VAP)

A collection of prefabs and assets designed to enhance **VRChat avatars** with advanced logic and interactive systems.

###
# Avatar Prefabs

Each avatar prefab should only be used **once per avatar**.

## Frame Time Prefab

Adds a **Frame Time system** to the avatar. This is required for most if not all prefabs!

### Available Properties

| Property | Animator Type | Expression Type | Synced | Description |
|-|-|-|-|-|
| FrameTime | Float | - | - | Stores the calculated frame time |
| VAP/Smooth/Gesture | Float | - | - | Smoothness factor used for gesture calculations |
| VAP/Smooth/Touch | Float | - | - | Smoothness factor used for touch interactions |
| VAP/Step Size/One | Float | - | - | Step size increment applied each second |

## Blink System Prefab

Adds **custom blinking** with the ability to **override the blink state** or **disable blinking entirely**.  
Create your blink logic or blend tree using the available properties below.

### Available Properties

| Property | Animator Type | Expression Type | Synced | Description |
|-|-|-|-|-|
| EarTwitchActive | Bool | Bool (Optional) | ✖ | Controls twitch feature |
| EarTwitchLeft | Bool | Bool | ✔ | Syncronizes left ear twitch |
| EarTwitchRight | Bool | Bool | ✔ | Syncronizes right ear twitch |
| VAP/Ear/Left_Twitch | Float | - | - | Output value used to drive the **left ear twitch** animation, this value is set to 100 to override other animations through a direct blend tree |
| VAP/Ear/Right_Twitch | Float | - | - | Output value used to drive the **right ear twitch** animation, this value is set to 100 to override other animations through a direct blend tree |

## Ear Twitch Prefab

Adds occasional ear twitches that trigger at random intervals.

### Available Properties

| Property | Animator Type | Expression Type | Synced | Description |
|-|-|-|-|-|
| Blink | Bool | Bool | ✔ | Syncronizes blink |
| BlinkActive | Bool | Bool (Optional) | ✖ | Controls blinking feature |
| VAP/Blink/Left | Float | - | - | Output value used to drive the **left eye blink** blendshape |
| VAP/Blink/Right | Float | - | - | Output value used to drive the **right eye blink** blendshape |
| VAP/Blink/LeftOverride | Float | - | - | Override value applied to `VAP/Blink/Left` output |
| VAP/Blink/RightOverride | Float | - | - | Override value applied to `VAP/Blink/Right` output |

## Logarithmic Gestures Prefab

> Requires the **Frame Time Prefab**.

Adds **blend tree–driven gesture detection** with smoothing and menu-based overrides. Create your gesture blend tree using the available properties. The menu-based overrides are optional.

### Available Properties

| Property | Animator Type | Expression Type | Synced | Description |
|-|-|-|-|-|
| GestureLeftOverride | Float | Integer (Optional) | ✔ | Overrides left gesture via the provided expression menu |
| GestureRightOverride | Float | Integer (Optional) | ✔ | Overrides right gesture via the provided expression menu |
| VAP/GestureLeft | Float | - | - | Output of gesture left |
| VAP/GestureRight | Float | - | - | Output of gesture right |
| VAP/GestureLeft/Weight | Float | - | - | Smoothed weight value for left gesture |
| VAP/GestureRight/Weight | Float | - | - | Smoothed weight value for right gesture |
| VAP/GestureLeft/Idle | Float | - | - | Smooth output when left gesture is **Idle** |
| VAP/GestureLeft/Fist | Float | - | - | Smooth output when left gesture is **Fist** |
| VAP/GestureLeft/Open | Float | - | - | Smooth output when left gesture is **Open** |
| VAP/GestureLeft/Point | Float | - | - | Smooth output when left gesture is **Point** |
| VAP/GestureLeft/Victory | Float | - | - | Smooth output when left gesture is **Victory** |
| VAP/GestureLeft/Rock&Roll | Float | - | - | Smooth output when left gesture is **Rock & Roll** |
| VAP/GestureLeft/Gun | Float | - | - | Smooth output when left gesture is **Gun** |
| VAP/GestureLeft/ThumbsUp | Float | - | - | Smooth output when left gesture is **Thumbs Up** |
| VAP/GestureRight/Fist | Float | - | - | Smooth output when right gesture is **Fist** |
| VAP/GestureRight/Open | Float | - | - | Smooth output when right gesture is **Open** |
| VAP/GestureRight/Point | Float | - | - | Smooth output when right gesture is **Point** |
| VAP/GestureRight/Victory | Float | - | - | Smooth output when right gesture is **Victory** |
| VAP/GestureRight/Rock&Roll | Float | - | - | Smooth output when right gesture is **Rock & Roll** |
| VAP/GestureRight/Gun | Float | - | - | Smooth output when right gesture is **Gun** |
| VAP/GestureRight/ThumbsUp | Float | - | - | Smooth output when right gesture is **Thumbs Up** |

## Viseme Tongue Movement Prefab

> Requires the **Frame Time Prefab**.

Adds **blend tree–driven tongue viseme movement**, separating it from the standard viseme blendshapes so they can be disabled independently. Create your viseme tongue blend tree using the available properties.

### Available Properties

| Property | Animator Type | Expression Type | Synced | Description |
|-|-|-|-|-|
| VAP/Viseme/TongueForward | Float | - | - | Smooth output for activating Tongue Forward blendshape |
| VAP/Viseme/TongueUp | Float | - | - | Smooth output for activating Tongue Up blendshape |

## Smooth Ear Grab Prefab

> Requires the **Frame Time Prefab**.

Adds **smooth ear grab interactions** to the avatar for face stretch, use the parameters `Ear/Left` & `Ear/Right` for your ear physbones. Implement face stretch on the gesture blend tree using the available properties.

### Available Properties

| Property | Animator Type | Expression Type | Synced | Description |
|-|-|-|-|-|
| VAP/Ear/Left_Stretch | Float | - | - | Smooth output for left ear stretch, only present when left ear is grabbed |
| VAP/Ear/Right_Stretch | Float | - | - | Smooth output for right ear stretch, only present when right is grabbed |

## Eye Poke System Prefab

> Requires the **Frame Time Prefab**.

Adds **smooth eye poke interactions** to the avatar using a contact sensor per eye. Use the available properties below for the interaction.

### Available Properties

| Property | Animator Type | Expression Type | Synced | Description |
|-|-|-|-|-|
| VAP/Touch/EyeLeft | Float | - | - | Smoothed proximity value of left eye contact sensor |
| VAP/Touch/EyeRight | Float | - | - | Smoothed proximity value of right eye contact sensor |
| Touch/EyeLeft | Float | - | - | Raw proximity value of left eye contact sensor |
| Touch/EyeRight | Float | - | - | Raw proximity value of right eye contact sensor |

## Foot Poke System Prefab

> Requires the **Frame Time Prefab**.

Adds **smooth foot poke interactions** to the avatar using a contact sensor per foot. Use the available properties below for the interaction.

### Available Properties

| Property | Animator Type | Expression Type | Synced | Description |
|-|-|-|-|-|
| VAP/Touch/FootLeft | Float | - | - | Smoothed proximity value of left foot contact sensor |
| VAP/Touch/FootRight | Float | - | - | Smoothed proximity value of right foot contact sensor |
| Touch/FootLeft | Float | - | - | Raw proximity value of left foot contact sensor |
| Touch/FootRight | Float | - | - | Raw proximity value of right foot contact sensor |

## Nose Boop System Prefab

> Requires the **Frame Time Prefab**.

Adds **smooth nose boop interaction** to the avatar using a contact sensor. Use the available properties below for the interaction.

### Available Properties

| Property | Animator Type | Expression Type | Synced | Description |
|-|-|-|-|-|
| VAP/Touch/Nose | Float | - | - | Smoothed proximity value of nose contact sensor|
| VAP/Touch/NoseTimer | Float | - | - | Timer how long the nose contact is held touched |
| Touch/Nose | Float | - | - | Raw proximity value of nose contact sensor |

## Heartbeat System Prefab

Adds a **heartbeat sound effect and visual display** to the avatar.

### Setup Instructions

- Position the **Heartbeat Sound** object at the avatar's chest.
- A **Contact Receiver** ensures the **Audio Source** only activates when needed. This helps avoid the VRChat limit of **three active audio sources per avatar**.
- Position the **Heartbeat Counter** object over the avatar's **left hand**. Remove the **VRCFury Armature Link** component if you want to attach the counter somewhere else.

###
# Asset Prefabs

## Bell Sound System Prefab

> Requires the **Frame Time Prefab**.

Adds **movement-based bell sounds** to an asset for more realistic motion effects.

### Setup Instructions

- Attach the **Bell Sound** object to the desired location using a **VRCFury Armature Link**.
- A **PhysBone component** is included as a sample; you may replace or modify it as needed.
- A **Chest Collider** is included to prevent the bell from clipping into the avatar's chest. Remove it if it is not required.