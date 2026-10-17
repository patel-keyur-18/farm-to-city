# How to build and install on the iPhone 13

Step by step, from the repository to the app running on your phone. Written for Unity 6000.3.26f1 (ADR 0029), Xcode 27.1 and a free Apple account (ADR 0007, ADR 0026).

**How the pieces fit, in Java terms:** Unity compiles our C# and converts it to C++ (IL2CPP), then writes out an **Xcode project**, much like a code generator writing sources that `javac` and Maven then compile and package. Xcode is the final compiler, signer and installer. So every build has two halves: **Unity → Xcode project**, then **Xcode → iPhone**.

## One-time setup

Do these once. Skip any you've already done.

1. **Git LFS** (large binary files such as images): `brew install git-lfs`, then `git lfs install`.
2. **Unity 6000.3.26f1** with *iOS Build Support*, installed through Unity Hub. If Hub offers a newer version, ignore it (ADR 0029).
3. **Xcode 27.1** from the App Store. Open it once and let it install its components.
4. **Your Apple ID in Xcode:** Xcode → Settings → Accounts → **+** → Apple ID. This creates a free *Personal Team*.
5. **On the iPhone:**
   - Connect it to the Mac with a cable, unlock it and tap **Trust This Computer**.
   - Turn on **Developer Mode**: Settings → Privacy & Security → Developer Mode → on. The phone restarts and asks you to confirm.

## Each build

### 1. Get the code

```
cd farm-to-city
git fetch
git checkout <the branch named in the test checklist>
git pull
git lfs pull
```

### 2. Unity: make the Xcode project

1. Open the project `unity/FarmToCity` from Unity Hub (version 6000.3.26f1).
2. Wait for the import to finish (the progress bar at the bottom right). The Console (Window → General → Console) must show no red errors.
3. File → **Build Profiles**. The active platform must be **iOS**. Under *Scene List*, only `Assets/Scenes/Bootstrap.unity` is ticked.
   - A *build profile* is like a Maven profile: the target platform plus its settings.
   - The bootstrap scene is nearly empty on purpose. All game objects and UI are created by code at start-up (ADR 0017).
4. Click **Build**. Choose the folder `unity/FarmToCity/Builds/iOS` (create it the first time; git ignores it). If Unity asks *Append* or *Replace*, choose **Replace**.
   - The build copies the repository's `content/*.json` into the app (you'll see `ContentBuildStep: copied … content files` in the Console).
   - The first build takes several minutes; later builds are faster.

### 3. Xcode: sign and install

1. Open `unity/FarmToCity/Builds/iOS/Unity-iPhone.xcodeproj` in Xcode.
2. In the left sidebar, click the **Unity-iPhone** project, then the **Unity-iPhone** target → **Signing & Capabilities**:
   - Tick **Automatically manage signing**.
   - **Team:** *Your Name (Personal Team)*.
   - **Bundle Identifier:** `com.keyurpatel.farmtocity`. If Xcode says the identifier isn't available, tell Claude: it has to change in Unity's Player Settings too.
3. At the top of the window, choose your **iPhone** as the run destination (not a simulator).
4. Press **Run** (⌘R). Xcode compiles (a few minutes the first time), installs and launches the app.
5. **The first time only**, the phone may refuse to open the app (*Untrusted Developer*): Settings → General → **VPN & Device Management** → your Apple ID → **Trust**. Then tap the app icon.

**Make signing stick (optional, recommended after the first success):** in Xcode, the Signing section shows your Team ID (10 characters). In Unity: Edit → Project Settings → Player → iOS tab → Other Settings → **Signing Team ID** = that ID, and tick **Automatically Sign**. From then on the Xcode project Unity generates is already signed, so step 3.2 is no longer needed.

## Free-account limits

- **The app stops opening after 7 days.** That's the free certificate expiring, not a bug. Build and run again from Xcode (step 3.4 is enough if the Xcode project is unchanged).
- At most 3 sideloaded apps on the phone at once, and 10 new app identifiers per 7 days. Keeping the same bundle identifier avoids using these up.

## Troubleshooting

| Symptom | Fix |
| --- | --- |
| Red errors in Unity's Console after pulling | Copy the first error to Claude. Don't build until the Console is clean |
| `git lfs` errors, or images show as small text files | Run `git lfs install`, then `git lfs pull` |
| Xcode: *No profiles for 'com.keyurpatel.farmtocity' were found* | Signing & Capabilities: tick Automatically manage signing and pick your Personal Team |
| Xcode: *iPhone is not available* / *Developer Mode disabled* | Unlock the phone, check Developer Mode is on, reconnect the cable |
| Xcode: *Could not launch … Untrusted Developer* | Trust your Apple ID on the phone (step 3.5) |
| The app shows a blank screen | Report it with the Xcode console output (View → Debug Area → Activate Console) |

## What Claude can do from the Mac

While the Unity Editor is open, Claude can compile, enter play mode, capture the Game view and start the Unity half of the build through the `unity` CLI (the Unity Pipeline package). Signing, installing and testing on the phone stay with you.
