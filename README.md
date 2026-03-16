# VRChat Azuki Prefabs (VAP)

A collection of prefabs and assets designed to enhance **VRChat avatars** with advanced animations and interactive systems.

###
# Avatar Prefabs

Each avatar prefab should only be used **once per avatar**.

<details>
<summary><h3>Frame Time Prefab</h3></summary>

Adds a **Frame Time system** to the avatar. This prefab is required for most other prefabs.

### Instructions
1. Drag and drop the prefab into the avatar's hierarchy.

### Available Properties

| Property | Animator Type | Expression Type | Synced | Description |
|-|-|-|-|-|
| FrameTime | Float | - | - | Stores the calculated frame time |
| VAP/Smooth/Gesture | Float | - | - | Smoothness factor used for gesture calculations |
| VAP/Smooth/Touch | Float | - | - | Smoothness factor used for touch interactions |
| VAP/Smooth/StepSizeOne | Float | - | - | Step size increment applied every second |

</details>

---

<details>
<summary><h3>Blink System Prefab</h3></summary>

Adds **custom blinking** with the ability to **override the blink state** or **disable blinking entirely**.

### Instructions
1. Drag and drop the prefab into the avatar's hierarchy.
2. Use the animator parameters `VAP/Blink/Left` and `VAP/Blink/Right` to drive the blink blendshapes.
3. Optionally, create an expression parameter and toggle for `BlinkActive` to disable blinking.
4. Optionally, use the animator parameters `VAP/Override/Blink/Left` and `VAP/Override/Blink/Right` for interactions such as eye pokes.

### Available Properties

| Property | Animator Type | Expression Type | Synced | Description |
|-|-|-|-|-|
| BlinkActive | Bool | Bool (Optional) | ✖ | Enables or disables the blinking feature |
| Blink | Bool | Bool | ✔ | Synchronizes the blink state |
| VAP/Blink/Left | Float | - | - | Output value used to drive the **left eye blink** blendshape |
| VAP/Blink/Right | Float | - | - | Output value used to drive the **right eye blink** blendshape |
| VAP/Override/Blink/Left | Float | - | - | Override value applied to `VAP/Blink/Left` when not blinking |
| VAP/Override/Blink/Right | Float | - | - | Override value applied to `VAP/Blink/Right` when not blinking |

</details>

---

<details>
<summary><h3>Ear Twitch Prefab</h3></summary>

Adds occasional **ear twitches** that trigger at random intervals.

### Instructions
1. Drag and drop the prefab into the avatar's hierarchy.
2. Use the animator parameters `VAP/Ear/Left_Twitch` and `VAP/Ear/Right_Twitch` to drive ear rotations.
3. Optionally, create an expression parameter and toggle for `EarTwitchActive` to disable ear twitches.

### Available Properties

| Property | Animator Type | Expression Type | Synced | Description |
|-|-|-|-|-|
| EarTwitchActive | Bool | Bool (Optional) | ✖ | Enables or disables the ear twitch feature |
| EarTwitchLeft | Bool | Bool | ✔ | Synchronizes the left ear twitch |
| EarTwitchRight | Bool | Bool | ✔ | Synchronizes the right ear twitch |
| VAP/Ear/Left_Twitch | Float | - | - | Output value used to drive the **left ear twitch** animation. This value is set to `100` to override other animations through a direct blend tree |
| VAP/Ear/Right_Twitch | Float | - | - | Output value used to drive the **right ear twitch** animation. This value is set to `100` to override other animations through a direct blend tree |

</details>

---

<details>
<summary><h3>Logarithmic Gestures Prefab</h3></summary>

> Requires the **Frame Time Prefab**.

Adds **blend tree–driven gesture detection** with smoothing and optional menu-based overrides. Create your gesture blend tree using the available properties.

### Instructions
1. Drag and drop the prefab into the avatar's hierarchy.
2. Use the animator parameters `VAP/GestureLeft/*` and `VAP/GestureRight/*` to drive gestures.
3. Optionally, use the assets `VAP Logarithmic Gestures Override Parameters`, `VAP Logarithmic Gestures Override Left Menu`, and `VAP Logarithmic Gestures Override Right Menu` to add gesture overrides to the expression menu.

### Available Properties

| Property | Animator Type | Expression Type | Synced | Description |
|-|-|-|-|-|
| GestureLeftOverride | Float | Integer (Optional) | ✔ | Overrides the left gesture using the provided expression menu |
| GestureRightOverride | Float | Integer (Optional) | ✔ | Overrides the right gesture using the provided expression menu |
| VAP/GestureLeft | Float | - | - | Output value representing the current left gesture |
| VAP/GestureRight | Float | - | - | Output value representing the current right gesture |
| VAP/GestureLeft/Weight | Float | - | - | Smoothed output value representing the left gesture weight |
| VAP/GestureRight/Weight | Float | - | - | Smoothed output value representing the right gesture weight |
| VAP/GestureLeft/Idle | Float | - | - | Smoothed output value when the left gesture is **Idle** |
| VAP/GestureLeft/Fist | Float | - | - | Smoothed output value when the left gesture is **Fist** |
| VAP/GestureLeft/Open | Float | - | - | Smoothed output value when the left gesture is **Open** |
| VAP/GestureLeft/Point | Float | - | - | Smoothed output value when the left gesture is **Point** |
| VAP/GestureLeft/Victory | Float | - | - | Smoothed output value when the left gesture is **Victory** |
| VAP/GestureLeft/Rock&Roll | Float | - | - | Smoothed output value when the left gesture is **Rock & Roll** |
| VAP/GestureLeft/Gun | Float | - | - | Smoothed output value when the left gesture is **Gun** |
| VAP/GestureLeft/ThumbsUp | Float | - | - | Smoothed output value when the left gesture is **Thumbs Up** |
| VAP/GestureRight/Fist | Float | - | - | Smoothed output value when the right gesture is **Fist** |
| VAP/GestureRight/Open | Float | - | - | Smoothed output value when the right gesture is **Open** |
| VAP/GestureRight/Point | Float | - | - | Smoothed output value when the right gesture is **Point** |
| VAP/GestureRight/Victory | Float | - | - | Smoothed output value when the right gesture is **Victory** |
| VAP/GestureRight/Rock&Roll | Float | - | - | Smoothed output value when the right gesture is **Rock & Roll** |
| VAP/GestureRight/Gun | Float | - | - | Smoothed output value when the right gesture is **Gun** |
| VAP/GestureRight/ThumbsUp | Float | - | - | Smoothed output value when the right gesture is **Thumbs Up** |

</details>

---

<details>
<summary><h3>Viseme Tongue Movement Prefab</h3></summary>

> Requires the **Frame Time Prefab**.

Adds **blend tree–driven tongue viseme movement**, separated from the standard viseme blendshapes so they can be disabled independently.

### Instructions
1. Drag and drop the prefab into the avatar's hierarchy.
2. Use the animator parameters `VAP/Viseme/TongueForward` and `VAP/Viseme/TongueUp` to drive the tongue viseme blendshapes.

### Available Properties

| Property | Animator Type | Expression Type | Synced | Description |
|-|-|-|-|-|
| VAP/Viseme/TongueForward | Float | - | - | Smoothed output value used to drive the **Tongue Forward** blendshape |
| VAP/Viseme/TongueUp | Float | - | - | Smoothed output value used to drive the **Tongue Up** blendshape |

</details>

---

<details>
<summary><h3>Smooth Ear Grab Prefab</h3></summary>

> Requires the **Frame Time Prefab**.

Adds **smooth ear grab interactions** for face stretching.

### Instructions
1. Drag and drop the prefab into the avatar's hierarchy.
2. Set the parameters `Ear/Left` and `Ear/Right` for the ear PhysBones.
3. Use the animator parameters `VAP/Ear/Left_Stretch` and `VAP/Ear/Right_Stretch` to drive face stretch blendshapes.

### Available Properties

| Property | Animator Type | Expression Type | Synced | Description |
|-|-|-|-|-|
| VAP/Ear/Left_Stretch | Float | - | - | Smoothed output value representing left ear stretch when the ear is grabbed |
| VAP/Ear/Right_Stretch | Float | - | - | Smoothed output value representing right ear stretch when the ear is grabbed |

</details>

---

<details>
<summary><h3>Tail Wag Speed Prefab</h3></summary>

Controls **tail wag speed** through a menu value or animator override. Wagging automatically stops when the tail is grabbed or when a pose is active. Use the parameter `Tail` for the tail PhysBone.

### Instructions
1. Drag and drop the prefab into the avatar's hierarchy.
2. Use the animator parameter `VAP/Tail/WagSpeed` as the multiplier for the tail wag animation.
3. Optionally, use the animator parameter `VAP/Override/Tail/WagSpeed` to additively increase the speed.

### Available Properties

| Property | Animator Type | Expression Type | Synced | Description |
|-|-|-|-|-|
| TailWagSpeed | Float | Float (Optional) | ✔ | Menu value used to control tail wag speed |
| VAP/Tail/WagSpeed | Float | - | - | Output value used as the tail wag animation speed multiplier |
| VAP/Override/Tail/WagSpeed | Float | - | - | Overrides the minimum wag speed of `VAP/Tail/WagSpeed` |

</details>

---

<details>
<summary><h3>Eye Poke System Prefab</h3></summary>

> Requires the **Frame Time Prefab**.

Adds **smooth eye poke interactions** using a contact sensor for each eye.

### Instructions
1. Drag and drop the prefab into the avatar's hierarchy.
2. Position the contact sensors over the avatar's eyes.
3. Use the animator parameters `VAP/Touch/EyeLeft` and `VAP/Touch/EyeRight`.

### Available Properties

| Property | Animator Type | Expression Type | Synced | Description |
|-|-|-|-|-|
| VAP/Touch/EyeLeft | Float | - | - | Smoothed proximity value from the left eye contact sensor |
| VAP/Touch/EyeRight | Float | - | - | Smoothed proximity value from the right eye contact sensor |
| Touch/EyeLeft | Float | - | - | Raw proximity value from the left eye contact sensor |
| Touch/EyeRight | Float | - | - | Raw proximity value from the right eye contact sensor |

</details>

---

<details>
<summary><h3>Foot Poke System Prefab</h3></summary>

> Requires the **Frame Time Prefab**.

Adds **smooth foot poke interactions** using a contact sensor for each foot.

### Instructions
1. Drag and drop the prefab into the avatar's hierarchy.
2. Position the contact sensors over the avatar's feet.
3. Use the animator parameters `VAP/Touch/FootLeft` and `VAP/Touch/FootRight`.

### Available Properties

| Property | Animator Type | Expression Type | Synced | Description |
|-|-|-|-|-|
| VAP/Touch/FootLeft | Float | - | - | Smoothed proximity value from the left foot contact sensor |
| VAP/Touch/FootRight | Float | - | - | Smoothed proximity value from the right foot contact sensor |
| Touch/FootLeft | Float | - | - | Raw proximity value from the left foot contact sensor |
| Touch/FootRight | Float | - | - | Raw proximity value from the right foot contact sensor |

</details>

---

<details>
<summary><h3>Nose Boop System Prefab</h3></summary>

> Requires the **Frame Time Prefab**.

Adds a **smooth nose boop interaction** using a contact sensor.

### Instructions
1. Drag and drop the prefab into the avatar's hierarchy.
2. Position the contact sensor over the avatar's nose.
3. Use the animator parameter `VAP/Touch/Nose`.
4. Optionally, use the animator parameter `VAP/Touch/NoseTimer` for an interaction.

### Available Properties

| Property | Animator Type | Expression Type | Synced | Description |
|-|-|-|-|-|
| VAP/Touch/Nose | Float | - | - | Smoothed proximity value from the nose contact sensor |
| VAP/Touch/NoseTimer | Float | - | - | Timer representing how long the nose contact is held |
| Touch/Nose | Float | - | - | Raw proximity value from the nose contact sensor |

</details>

---

<details>
<summary><h3>Toe Curl System Prefab</h3></summary>

Adds a **toe curl feature** using two contact sensors and two rotation constraints.

### Instructions
1. Drag and drop the prefab into the avatar's hierarchy.
2. Add a game object to each **foot** of your avatar without any transforms (e.g., `Curl_L` and `Curl_R`).
3. Update the `Root Transform` of the contact sensors of `Left Curl` and `Right Curl` to your corresponding `Curl_L` and `Curl_R` game objects.
4. Update the `Target Transform` of the rotation constraints of `Left Curl` and `Right Curl` to your corresponding `Curl_L` and `Curl_R` game objects.
5. Update the `Source Transform` of the rotation constraints of `Left Curl` and `Right Curl` to the corresponding **foot** of your avatar. This will prevent the contact sensors from rotating.

### Available Properties

| Property | Animator Type | Expression Type | Synced | Description |
|-|-|-|-|-|
| Touch/FootLCurl | Float | - | - | Raw proximity value from the left toe curl contact sensor |
| Touch/FootRCurl | Float | - | - | Raw proximity value from the right toe curl contact sensor |

</details>

---

<details>
<summary><h3>Heartbeat System Prefab</h3></summary>

Adds a **heartbeat sound effect and visual display**.

### Instructions
1. Drag and drop the prefab into the avatar's hierarchy.
2. Position the **Heartbeat Sound** object at the avatar's chest.
3. A **Contact Receiver** ensures the **Audio Source** only activates when needed. This helps avoid the VRChat limit of **three active audio sources per avatar**.
4. Position the **Heartbeat Counter** object over the avatar's **left hand**. Remove the **VRCFury Armature Link** component if you want to attach the counter elsewhere.

</details>

---

<details>
<summary><h3>GoGo Loco Simplified Prefab</h3></summary>

Provides **customized GoGo Loco controllers and menus** with simplified menu options and additional improvements.

### Instructions
1. Drag and drop the prefab into the avatar's hierarchy.
2. GoGo Loco version `1.8.6` must be installed in the project.

### Features
- Fixes the **Index controller finger tracking issue**
- Toggle for **feral movement animations**
- Simplified and improved **menu options**

</details>

###
# Asset Prefabs

<details>
<summary><h3>Bell Sound System Prefab</h3></summary>

> Requires the **Frame Time Prefab**.

Adds **movement-based bell sounds** to an asset for more realistic motion effects.

### Instructions
1. Drag and drop the prefab into the asset's hierarchy.
2. Attach the **Bell Sound** game object to the desired location using a **VRCFury Armature Link**. It will be attached at the root of the selected game object.
3. A **PhysBone component** is included as a sample and can be modified or replaced as needed.
4. A **Chest Collider** is included to prevent the bell from clipping into the avatar's chest. Remove it if it is not required.

</details>