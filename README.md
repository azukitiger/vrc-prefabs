<img src="./Website/banner.png"/>

# Avatar Prefabs
A collection of prefabs and assets designed to enhance **VRChat avatars** with advanced animations and interactive systems.

<details>
<summary><h2>Frame Time Prefab</h2></summary>

Adds a **Frame Time system** to the avatar. This prefab is required for most other prefabs.

### Instructions
1. Drag and drop the prefab into the avatar's hierarchy.

### Output Animator Parameters

| Property | Animator Type | Description |
|-|-|-|
| FrameTime | Float | Stores the calculated frame time |
| VAP/Smooth/Gesture | Float | Smoothness factor used for gesture calculations |
| VAP/Smooth/Touch | Float | Smoothness factor used for touch interactions |
| VAP/Smooth/StepSizeOne | Float | Step size increment applied every second |

</details>

---

<details>
<summary><h2>Blink System Prefab</h2></summary>

Adds a **smooth blink animation** that follows the eyelid values from VRChat's face tracking, with an occasional **double blink**, with the ability to **override the blink state** or **disable blinking entirely**.

### Features
- A blink plays whenever both eyelids close past halfway (`EyeLidLeft` and `EyeLidRight` below `0.5`), so it follows VRChat's eyelid data.
- Each blink has a **25% chance** to be followed by a second blink.
- Includes **Face Tracking Parameter Behaviors** that output `EyeLidLeft` and `EyeLidRight`, so no setup is needed on the avatar descriptor.

### Instructions
1. Drag and drop the prefab into the avatar's hierarchy.
2. Use the animator parameters `VAP/BlinkLeft` and `VAP/BlinkRight` to drive the blink blendshapes.
3. Optionally, create an expression parameter and toggle for `BlinkActive` to disable blinking.
4. Optionally, use the animator parameters `VAP/BlinkLeftOverride` and `VAP/BlinkRightOverride` for interactions such as eye pokes.

### Optional Expression Parameters

| Property | Animator Type | Expression Type | Synced | Description |
|-|-|-|-|-|
| BlinkActive | Bool | Bool | ✔ | Enables or disables the blinking feature. On by default |

### Output Animator Parameters

| Property | Animator Type | Description |
|-|-|-|
| VAP/BlinkLeft | Float | Output value used to drive the **left eye blink** blendshape. `1` is closed |
| VAP/BlinkRight | Float | Output value used to drive the **right eye blink** blendshape. `1` is closed |
| EyeLidLeft | Float | Left eyelid from face tracking: `(1 - EyeClosedLeft) * 0.75 + EyeWideLeft * 0.25`. `0` is closed, `0.75` is open and `1` is wide |
| EyeLidRight | Float | Right eyelid from face tracking: `(1 - EyeClosedRight) * 0.75 + EyeWideRight * 0.25`. `0` is closed, `0.75` is open and `1` is wide |

### Input Animator Parameters

| Property | Animator Type | Description |
|-|-|-|
| VAP/BlinkLeftOverride | Float | Override value applied to `VAP/BlinkLeft` when not blinking |
| VAP/BlinkRightOverride | Float | Override value applied to `VAP/BlinkRight` when not blinking |

</details>

---

<details>
<summary><h2>Ear Twitch Prefab</h2></summary>

Adds occasional **ear twitches** that trigger at random intervals.

### Instructions
1. Drag and drop the prefab into the avatar's hierarchy.
2. Use the animator parameters `VAP/Ear/Left_Twitch` and `VAP/Ear/Right_Twitch` to drive ear rotations.
3. Optionally, create an expression parameter and toggle for `EarTwitchActive` to disable ear twitches.

### Included Expression Parameters

| Property | Animator Type | Expression Type | Synced | Description |
|-|-|-|-|-|
| EarTwitchLeft | Bool | Bool | ✔ | Synchronizes the left ear twitch |
| EarTwitchRight | Bool | Bool | ✔ | Synchronizes the right ear twitch |

### Optional Expression Parameters

| Property | Animator Type | Expression Type | Synced | Description |
|-|-|-|-|-|
| EarTwitchActive | Bool | Bool | ✖ | Enables or disables the ear twitch feature |

### Output Animator Parameters

| Property | Animator Type | Description |
|-|-|-|
| VAP/Ear/Left_Twitch | Float | Output value used to drive the **left ear twitch** animation. This value is set to `100` to override other animations through a direct blend tree |
| VAP/Ear/Right_Twitch | Float | Output value used to drive the **right ear twitch** animation. This value is set to `100` to override other animations through a direct blend tree |

</details>

---

<details>
<summary><h2>Logarithmic Gestures Prefab</h2></summary>

> Requires the **Frame Time Prefab**.

Adds **blend tree–driven gesture detection** with smoothing and optional menu-based overrides. Create your gesture blend tree using the available Parameters.

### Instructions
1. Drag and drop the prefab into the avatar's hierarchy.
2. Use the animator parameters `VAP/GestureLeft/*` and `VAP/GestureRight/*` to drive gestures.
3. Optionally, use the assets `VAP Logarithmic Gestures Override Parameters`, `VAP Logarithmic Gestures Override Left Menu`, and `VAP Logarithmic Gestures Override Right Menu` to add gesture overrides to the expression menu.

### Included Expression Parameters

| Property | Animator Type | Expression Type | Synced | Description |
|-|-|-|-|-|
| QuestControllers | Float | Boolean  | ✔ | Add with VRCFury to disable victory gestures & trigger weights

### Optional Expression Parameters

| Property | Animator Type | Expression Type | Synced | Description |
|-|-|-|-|-|
| GestureLeftOverride | Float | Integer | ✔ | Overrides the left gesture using the provided expression menu |
| GestureRightOverride | Float | Integer | ✔ | Overrides the right gesture using the provided expression menu |

### Output Animator Parameters

| Property | Animator Type | Description |
|-|-|-|
| VAP/GestureLeft | Float | Output value representing the current left gesture |
| VAP/GestureRight | Float | Output value representing the current right gesture |
| VAP/GestureLeft/Weight | Float | Smoothed output value representing the left gesture weight |
| VAP/GestureRight/Weight | Float | Smoothed output value representing the right gesture weight |
| VAP/GestureLeft/Idle | Float | Smoothed output value when the left gesture is **Idle** |
| VAP/GestureLeft/Fist | Float | Smoothed output value when the left gesture is **Fist** |
| VAP/GestureLeft/Open | Float | Smoothed output value when the left gesture is **Open** |
| VAP/GestureLeft/Point | Float | Smoothed output value when the left gesture is **Point** |
| VAP/GestureLeft/Victory | Float | Smoothed output value when the left gesture is **Victory** |
| VAP/GestureLeft/Rock&Roll | Float | Smoothed output value when the left gesture is **Rock & Roll** |
| VAP/GestureLeft/Gun | Float | Smoothed output value when the left gesture is **Gun** |
| VAP/GestureLeft/ThumbsUp | Float | Smoothed output value when the left gesture is **Thumbs Up** |
| VAP/GestureRight/Fist | Float | Smoothed output value when the right gesture is **Fist** |
| VAP/GestureRight/Open | Float | Smoothed output value when the right gesture is **Open** |
| VAP/GestureRight/Point | Float | Smoothed output value when the right gesture is **Point** |
| VAP/GestureRight/Victory | Float | Smoothed output value when the right gesture is **Victory** |
| VAP/GestureRight/Rock&Roll | Float | Smoothed output value when the right gesture is **Rock & Roll** |
| VAP/GestureRight/Gun | Float | Smoothed output value when the right gesture is **Gun** |
| VAP/GestureRight/ThumbsUp | Float | Smoothed output value when the right gesture is **Thumbs Up** |

</details>

---

<details>
<summary><h2>Smooth Ear Grab Prefab</h2></summary>

> Requires the **Frame Time Prefab**.

Adds **smooth ear grab interactions** for face stretching.

### Instructions
1. Drag and drop the prefab into the avatar's hierarchy.
2. Set the parameters `Ear/Left` and `Ear/Right` for the ear PhysBones.
3. Use the animator parameters `VAP/Ear/Left_Stretch` and `VAP/Ear/Right_Stretch` to drive face stretch blendshapes.

### Output Animator Parameters

| Property | Animator Type | Description |
|-|-|-|
| VAP/Ear/Left_Stretch | Float | Smoothed output value representing left ear stretch when the ear is grabbed |
| VAP/Ear/Right_Stretch | Float | Smoothed output value representing right ear stretch when the ear is grabbed |

</details>

---

<details>
<summary><h2>Tail Wag Speed Prefab</h2></summary>

Controls **tail wag speed** through a menu value or animator override. Wagging automatically stops when the tail is grabbed or when a pose is active. Use the parameter `Tail` for the tail PhysBone.

### Instructions
1. Drag and drop the prefab into the avatar's hierarchy.
2. Use the animator parameter `VAP/Tail/WagSpeed` as the multiplier for the tail wag animation.
3. Optionally, use the animator parameter `VAP/Override/Tail/WagSpeed` to additively increase the speed.

### Optional Expression Parameters

| Property | Animator Type | Expression Type | Synced | Description |
|-|-|-|-|-|
| TailWagSpeed | Float | Float | ✔ | Menu value used to control tail wag speed |

### Output Animator Parameters

| Property | Animator Type | Description |
|-|-|-|
| VAP/Tail/WagSpeed | Float | Output value used as the tail wag animation speed multiplier |

### Input Animator Parameters

| Property | Animator Type | Description |
|-|-|-|
| VAP/Override/Tail/WagSpeed | Float | Overrides the minimum wag speed of `VAP/Tail/WagSpeed` |

</details>

---

<details>
<summary><h2>Eye Poke System Prefab</h2></summary>

> Requires the **Frame Time Prefab**.

Adds **smooth eye poke interactions** using a contact sensor for each eye.

### Instructions
1. Drag and drop the prefab into the avatar's hierarchy.
2. Position the contact sensors over the avatar's eyes.
3. Use the animator parameters `VAP/Touch/EyeLeft` and `VAP/Touch/EyeRight`.

### Output Animator Parameters

| Property | Animator Type | Description |
|-|-|-|
| VAP/Touch/EyeLeft | Float | Smoothed proximity value from the left eye contact sensor |
| VAP/Touch/EyeRight | Float | Smoothed proximity value from the right eye contact sensor |
| Touch/EyeLeft | Float | Raw proximity value from the left eye contact sensor |
| Touch/EyeRight | Float | Raw proximity value from the right eye contact sensor |

</details>

---

<details>
<summary><h2>Foot Poke System Prefab</h2></summary>

> Requires the **Frame Time Prefab**.

Adds **smooth foot poke interactions** using a contact sensor for each foot.

### Instructions
1. Drag and drop the prefab into the avatar's hierarchy.
2. Position the contact sensors over the avatar's feet.
3. Use the animator parameters `VAP/Touch/FootLeft` and `VAP/Touch/FootRight`.

### Output Animator Parameters

| Property | Animator Type | Description |
|-|-|-|
| VAP/Touch/FootLeft | Float | Smoothed proximity value from the left foot contact sensor |
| VAP/Touch/FootRight | Float | Smoothed proximity value from the right foot contact sensor |
| Touch/FootLeft | Float | Raw proximity value from the left foot contact sensor |
| Touch/FootRight | Float | Raw proximity value from the right foot contact sensor |

</details>

---

<details>
<summary><h2>Nose Boop System Prefab</h2></summary>

> Requires the **Frame Time Prefab**.

Adds a **smooth nose boop interaction** using a contact sensor.

### Instructions
1. Drag and drop the prefab into the avatar's hierarchy.
2. Position the contact sensor over the avatar's nose.
3. Use the animator parameter `VAP/Touch/Nose`.
4. Optionally, use the animator parameter `VAP/Touch/NoseTimer` for an interaction.

### Output Animator Parameters

| Property | Animator Type | Description |
|-|-|-|
| VAP/Touch/Nose | Float | Smoothed proximity value from the nose contact sensor |
| VAP/Touch/NoseTimer | Float | Timer representing how long the nose contact is held |
| Touch/Nose | Float | Raw proximity value from the nose contact sensor |

</details>

---

<details>
<summary><h2>Toe Curl System Prefab</h2></summary>

Adds a **toe curl feature** using two contact sensors and two rotation constraints.

### Instructions
1. Drag and drop the prefab into the avatar's hierarchy.
2. Add a game object to each **foot** of your avatar without any transforms (e.g., `Curl_L` and `Curl_R`).
3. Update the `Root Transform` of the contact sensors of `Left Curl` and `Right Curl` to your corresponding `Curl_L` and `Curl_R` game objects.
4. Update the `Target Transform` of the rotation constraints of `Left Curl` and `Right Curl` to your corresponding `Curl_L` and `Curl_R` game objects.
5. Update the `Source Transform` of the rotation constraints of `Left Curl` and `Right Curl` to the corresponding **foot** of your avatar. This will prevent the contact sensors from rotating.

### Output Animator Parameters

| Property | Animator Type | Description |
|-|-|-|
| Touch/FootLCurl | Float | Raw proximity value from the left toe curl contact sensor |
| Touch/FootRCurl | Float | Raw proximity value from the right toe curl contact sensor |

</details>

---

<details>
<summary><h2>Heartbeat System Prefab</h2></summary>

Adds a **heartbeat sound effect and visual display**.

### Features
- Uses a synced `float (7 bits)` for both Expression Menu–controlled and OSC values.
- Uses a synced `bool (1 bit)` to toggle the heart rate counter.

### Instructions
1. Drag and drop the prefab into the avatar's hierarchy.
2. Position the **Heartbeat Sound** object at the avatar's chest.
3. A **Contact Receiver** ensures the **Audio Source** only activates when needed. This helps avoid the VRChat limit of **three active audio sources per avatar**.
4. Position the **Heartbeat Counter** object over the avatar's **left hand**. Remove the **VRCFury Armature Link** component if you want to attach the counter elsewhere.

</details>

---

<details>
<summary><h2>GoGo Loco Simplified Prefab</h2></summary>

Provides **customized GoGo Loco controllers and menus** with simplified menu options and additional improvements.

### Features
- Fixes the **Index controller finger tracking issue**
- Toggle for **feral movement animations**
- Simplified and improved **menu options**

### Instructions
1. Drag and drop the prefab into the avatar's hierarchy.
2. GoGo Loco version `1.8.6` must be installed in the project.

</details>

---

<details>
<summary><h2>Face Tracking Prefab</h2></summary>

> Requires the **Frame Time Prefab**. Works with the **Blink System Prefab**, **Eye Rotation Prefab** & **Pupil Focus System Prefab**.

VRChat now drives the **Unified Expressions** blendshapes and smooths them through the avatar descriptor's **Blendshapes Sets**, so this prefab no longer animates those blendshapes or applies smoothing. It handles what VRChat doesn't: the menu toggles, emulation, combined expression values, mouth corrections and the eye tracking sensitivity setting.

### Features
- Uses `13 synced bits`.
- **Menu** under `Body Controls/Face Tracking` to toggle eye tracking, lip tracking, dilation tracking and **Disable Gestures**, with a **Settings** submenu for **Eye Sensitivity** and **Brow & Squint Emulation**.
- **Brow & Squint Emulation** for headsets that don't track brows or squint: brows follow nose sneer, the lower lip raiser and smile/frown, and squint follows smile and eyelid.
- **Eyelids**: with eye tracking on, `VAP/FT/EyeLidLeft` and `VAP/FT/EyeLidRight` follow the tracked eyelids. With it off, they follow the **Blink System Prefab**.
- **Pupil dilation**: uses tracked dilation when **Dilation Tracking** is on. Otherwise the pupils are emulated from expressions (smile dilates, anger constricts) and from the **Pupil Focus System Prefab** if it is installed.
- **Eye Sensitivity** (radial, default `0.6`): while eye tracking is on, eye look can be scaled from **1×** to **2×**. Used by the **Eye Rotation Prefab**.
- **Mouth corrections** applied to your `FaceTracking` Blendshapes Set.
- **Corrective blendshapes are triggered automatically**: any blendshape named after two blendshapes of your `FaceTracking` set followed by `Corrective` (ex. `MouthClosed` + `LipPucker` → `MouthClosedLipPuckerCorrective`) gets an **Apply to Blendshape** behavior at build time, with the expression `KeyA * KeyB`. When several tracking keys use the same blendshape, the strongest pair wins (`max(LipPuckerUpperLeft * MouthClosed, ...)`). Add the **Face Tracking Corrective Behaviors** component and pick the set: it lists them live, and any can be unchecked.
- Includes **Face Tracking Parameter Behaviors** for every input parameter, so no setup is needed for them on the avatar descriptor.

### Instructions
1. Drag and drop the prefab into the avatar's hierarchy.
2. Name the face blendshapes on your avatar exactly as VRChat's [tracking keys](https://vrc-beta-docs.netlify.app/avatars/face-tracking/osc) (ex. `JawOpen`, `EyeClosedLeft`).
3. On the **VRC Avatar Descriptor**, set:
   - **LipSync** > **Mode**: `Viseme Blend Shape Face Tracking`
   - **Eye Look**: enabled, with **Eye Gaze Mode** set to `Face Tracking` and **Eyelid Type** set to `None`
   - **Face Tracking**: enabled, with **Use All Tracking Keys** on, your face mesh in **Face Tracking Mesh**, **Viseme Dampen** set to `Dampen Tracking` and **Dampen Amount** set to `0.6`
4. Create a **Blendshapes Set** on the **VRC Avatar Descriptor** (under **Face Tracking**) named exactly `FaceTracking`, and map your face blendshapes in it. The prefab doesn't include one, and its Parameter Behaviors only work on a set with this name.
5. Optionally, add corrective blendshapes to your face mesh named `[Blendshape][Blendshape]Corrective` (ex. `MouthClosedLipPuckerCorrective`). They're picked up automatically by the **Face Tracking Corrective Behaviors** component.
6. Optionally, the animator parameters `VAP/FT/*` can be used to animate additional blendshapes or bones (ex. ears) from a separate FX controller.

### Included Expression Parameters

| Property | Animator Type | Expression Type | Synced | Description |
|-|-|-|-|-|
| EyeTrackingActive | Float | Boolean | ✔ | Toggle for enabling eye tracking |
| LipTrackingActive | Float | Boolean | ✔ | Toggle for enabling lip tracking |
| FT/EyeDilationEnabled | Float | Boolean | ✔ | Toggle for using tracked pupil dilation instead of emulated dilation |
| FT/FacialExpressionsDisabled | Float | Boolean | ✔ | Toggle for disabling gesture based expressions |
| FT/FaceTrackingEmulation | Float | Boolean | ✔ | Toggle for brow & squint emulation. On by default |
| FT/EyeSensitivity | Float | Float | ✔ | Eye look sensitivity while eye tracking is on |
| FT/State/TrackingActive | Float | Boolean | ✖ | Remembers whether tracking was on, so gestures are only switched when tracking is turned on or off |

<a name="vrcft-input"></a>
### Input Animator Parameters

Set by the prefab's **Face Tracking Parameter Behaviors**.

| Property | Animator Type | Expression |
|-|-|-|
| VAP/FT/EyeSquintLeftForward | Float | `EyeSquintLeft` |
| VAP/FT/EyeSquintRightForward | Float | `EyeSquintRight` |
| VAP/FT/BrowExpressionLeftForward | Float | `BrowInnerUpLeft - BrowLowererLeft` |
| VAP/FT/BrowExpressionRightForward | Float | `BrowInnerUpRight - BrowLowererRight` |
| VAP/FT/PupilDilationForward | Float | `(EyeDilationLeft + EyeDilationRight - EyeConstrictLeft - EyeConstrictRight) / 4 + 0.5` |
| EyeLidLeft | Float | From the **Blink System Prefab** |
| EyeLidRight | Float | From the **Blink System Prefab** |
| VAP/BlinkLeft | Float | From the **Blink System Prefab**. Used for the eyelids when eye tracking is off |
| VAP/BlinkRight | Float | From the **Blink System Prefab**. Used for the eyelids when eye tracking is off |
| Touch/PupilFocus | Float | Optional. Set by the **Pupil Focus System Prefab** |

### Output Animator Parameters

| Property | Animator Type | Description |
|-|-|-|
| VAP/FT/EyeLidLeft | Float | Left eyelid, tracked or from blinking. `0` is closed, `0.75` is open and `1` is wide |
| VAP/FT/EyeLidRight | Float | Right eyelid, tracked or from blinking. `0` is closed, `0.75` is open and `1` is wide |
| VAP/FT/EyeSquintLeft | Float | Left squint, tracked or emulated |
| VAP/FT/EyeSquintRight | Float | Right squint, tracked or emulated |
| VAP/FT/BrowExpressionLeft | Float | Left brow, tracked or emulated. `1` is raised and `-1` is lowered |
| VAP/FT/BrowExpressionRight | Float | Right brow, tracked or emulated. `1` is raised and `-1` is lowered |
| VAP/FT/PupilDilation | Float | Pupil dilation, tracked or emulated |
| VAP/FT/Smile | Float | Combined smile expression value. `1` represents a smile and `-1` represents a frown |
| VAP/FT/Brow | Float | Combined eyebrow expression value. `1` represents raised brows and `-1` represents lowered brows |
| VAP/FT/Angry | Float | Combined anger expression value. `1` represents a lowered brow with nose sneer |
| VAP/FT/NoseSneer | Float | `(NoseSneerLeft + NoseSneerRight) / 2` |
| VAP/FT/SmileFrownLeft | Float | `max(MouthCornerPullLeft, MouthCornerSlantLeft * 0.3) - MouthFrownLeft` |
| VAP/FT/SmileFrownRight | Float | `max(MouthCornerPullRight, MouthCornerSlantRight * 0.3) - MouthFrownRight` |
| VAP/FT/MouthRaiserLower | Float | `MouthRaiserLower` |
| VAP/FT/MouthX | Float | `(MouthUpperRight + MouthLowerRight - MouthUpperLeft - MouthLowerLeft) / 2` |
| VAP/FT/JawX | Float | `JawRight - JawLeft` |
| VAP/FT/JawOpen | Float | `JawOpen` |
| VAP/FT/TongueOut | Float | `TongueOut` |
| VAP/FT/TongueX | Float | `TongueRight - TongueLeft`, limited to `±(0.5 + TongueOut * 0.5)` |
| VAP/FT/TongueY | Float | `TongueUp - TongueDown`, limited by how far the tongue is out |
| VAP/FT/TongueTwist | Float | `TongueTwistRight - TongueTwistLeft` |
| VAP/EyeSensitivityInput | Float | Eye look sensitivity for the **Eye Rotation Prefab**. `0` while eye tracking is off |

</details>

---

<details>
<summary><h2>Eye Rotation Prefab</h2></summary>

> Works with the **Face Tracking Prefab**, which provides the eye tracking toggle and **Eye Sensitivity**.

Rotates the eyes from VRChat's eye look data, through both the **eye bones** and **eye blendshapes**.

### Features
- Includes **Face Tracking Parameter Behaviors** that output `EyeLeftX`, `EyeRightX` and `EyeY`, so no setup is needed on the avatar descriptor.
- Applies the **Eye Sensitivity** from the VRCFT prefab: from **1×** up to **2×** while eye tracking is on.
- **Eye bones**: drives the humanoid eye muscles through the **Additive** controller while eye tracking is on.
- **Eye blendshapes**: drives the `EyeLook*` blendshapes and the `EyeLook*Rotate` blendshapes on the `Body` mesh from the FX controller.

### Instructions
1. Drag and drop the prefab into the avatar's hierarchy.
2. Adjust the avatar's rig under **Muscles & Settings** for `Eye Down-Up` and `Eye In-Out` rotations.
3. Optionally, use the animator parameters `VAP/EyeLook/EaseOut`, `VAP/EyeLook/LeftVerticalEaseOut` and `VAP/EyeLook/RightVerticalEaseOut` to fade out the `EyeLook*` blendshapes.

### Input Animator Parameters

| Property | Animator Type | Description |
|-|-|-|
| EyeLeftX | Float | Set by the prefab: `EyeLookInLeft - EyeLookOutLeft` |
| EyeRightX | Float | Set by the prefab: `EyeLookOutRight - EyeLookInRight` |
| EyeY | Float | Set by the prefab: `(EyeLookUpLeft + EyeLookUpRight - EyeLookDownLeft - EyeLookDownRight) / 2` |
| VAP/EyeSensitivityInput | Float | Set by the **VRCFT** prefab. `0` is 1× and `1` is 2× |
| VAP/EyeLook/EaseOut | Float | Optional. `1` turns off all the `EyeLook*` blendshapes |
| VAP/EyeLook/LeftVerticalEaseOut | Float | Optional. `1` turns off the left eye's up/down `EyeLook*` blendshapes |
| VAP/EyeLook/RightVerticalEaseOut | Float | Optional. `1` turns off the right eye's up/down `EyeLook*` blendshapes |

### Output Animator Parameters

| Property | Animator Type | Description |
|-|-|-|
| VAP/EyeLeftX | Float | Horizontal look of the left eye, with sensitivity applied |
| VAP/EyeRightX | Float | Horizontal look of the right eye, with sensitivity applied |
| VAP/EyeY | Float | Vertical look shared by both eyes, with sensitivity applied |

</details>

---

<details>
<summary><h2>Pupil Focus System Prefab</h2></summary>

> Requires the **Frame Time Prefab**.

Adds a **Face Proximity contact receiver** that drives a **VRC Rotation Constraint** on the avatar's eyes to constrict the pupils when something gets close to the face.

### Instructions
1. Drag and drop the prefab into the avatar's hierarchy.
2. Position the contact receiver in front of the avatar's face, between the eyes.
3. Use the animator parameter `VAP/Touch/PupilFocus`.

### Output Animator Parameters

| Property | Animator Type | Description |
|-|-|-|
| VAP/Touch/PupilFocus | Float | Smoothed proximity value from the face contact sensor |
| Touch/PupilFocus | Float | Raw proximity value from the face contact sensor |

</details>

---

<details>
<summary><h2>Pupil Dilation Smoothing Prefab</h2></summary>

Combines **pupil dilation** from three sources into one value per eye:
- **User dilation**: a value the user sets, per eye (ex. from the **Expression Menu**).
- **Input dilation**: dilation from an input (ex. **gestures** or **face tracking**).
- **Closed eye dilation**: additional dilation that builds up while an eye is closed.

The prefab doesn't set any inputs itself: feed them from your own animator, menu or Face Tracking Parameter Behaviors.

### Instructions
1. Drag and drop the prefab into the avatar's hierarchy.
2. Feed the input parameters below, and optionally the user dilation parameters.
3. Use the animator parameters `VAP/Pupil/LeftDilation` and `VAP/Pupil/RightDilation` to animate the pupils.

### Optional Expression Parameters

| Property | Animator Type | Expression Type | Synced | Description |
|-|-|-|-|-|
| LeftDilationSet | Float | Float | Optional | User dilation for the left eye: `0` is small (default), `0.25` is default and `1` is high |
| RightDilationSet | Float | Float | Optional | User dilation for the right eye: `0` is small (default), `0.25` is default and `1` is high |

### Output Animator Parameters

| Property | Animator Type | Description |
|-|-|-|
| VAP/Pupil/LeftDilation | Float | Combined left pupil dilation |
| VAP/Pupil/RightDilation | Float | Combined right pupil dilation |

### Input Animator Parameters

| Property | Animator Type | Description |
|-|-|-|
| VAP/Pupil/DilationSmoothedInput | Float | Input dilation (ex. from gestures or face tracking) |
| VAP/Pupil/LeftEyeClosedInput | Float | Left eye closed amount. Above `0.9` adds closed eye dilation |
| VAP/Pupil/RightEyeClosedInput | Float | Right eye closed amount. Above `0.9` adds closed eye dilation |

</details>

---

<details>
<summary><h2>Raycast Floor Physbone Collider Prefab</h2></summary>

Uses a raycast component to set the floor PhysBone collider.

### Features
- Uses a synced, saved `bool` menu toggle to enable or disable the floor collider. Enabled by default.

### Instructions
1. Drag and drop the prefab into the avatar's hierarchy.
2. Add the **Floor Physbone Collider** object to the **Colliders** list of any PhysBone components that should collide with the floor.

### Included Expression Parameters

| Property | Animator Type | Expression Type | Synced | Description |
|-|-|-|-|-|
| FloorCollider | Bool | Bool | ✔ | Menu toggle used to enable or disable the floor collider |

</details>

---

<details>
<summary><h2>Raycast Wall Physbone Collider Prefab</h2></summary>

Uses 2 raycast components aiming outwards (left and right) to set the wall PhysBone collider in the right spot.

### Features
- Uses a synced `bool` menu toggle to enable or disable the wall collider. Disabled by default for performance.

### Instructions
1. Drag and drop the prefab into the avatar's hierarchy.
2. Add the **Wall Physbone Collider** object to the **Colliders** list of any PhysBone components that should collide with walls.

### Included Expression Parameters

| Property | Animator Type | Expression Type | Synced | Description |
|-|-|-|-|-|
| WallCollider | Bool | Bool | ✔ | Menu toggle used to enable or disable the wall collider |

</details>

###
# Asset Prefabs

<details>
<summary><h2>Bell Sound System Prefab</h2></summary>

> Requires the **Frame Time Prefab**.

Adds **movement-based bell sounds** to an asset for more realistic motion effects.

### Instructions
1. Drag and drop the prefab into the asset's hierarchy.
2. Attach the **Bell Sound** game object to the desired location using a **VRCFury Armature Link**. It will be attached at the root of the selected game object.
3. A **PhysBone component** is included as a sample and can be modified or replaced as needed.
4. A **Chest Collider** is included to prevent the bell from clipping into the avatar's chest. Remove it if it is not required.

</details>