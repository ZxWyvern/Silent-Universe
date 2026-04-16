![Unity](https://img.shields.io/badge/Unity-000000.svg?style=for-the-badge&logo=unity&logoColor=white) ![VContainer](https://img.shields.io/badge/VContainer-000000?style=for-the-badge&logo=unity&logoColor=white) ![URP](https://img.shields.io/badge/URP-black?style=for-the-badge&logo=unity&logoColor=white) ![Assembly Definition](https://img.shields.io/badge/ASMDEF-Unity%20Modular-black?style=for-the-badge&logo=unity&logoColor=white) ![Render Graph](https://img.shields.io/badge/Render%20Graph-Unity%206-blue?style=for-the-badge&logo=unity&logoColor=white) ![Input System](https://img.shields.io/badge/Unity-Input%20System-orange?style=for-the-badge&logo=unity&logoColor=white) ![Dependency Injection](https://img.shields.io/badge/DI-Dependency%20Injection-brightgreen?style=for-the-badge&logo=google-cloud&logoColor=white) ![HLSL](https://img.shields.io/badge/HLSL-Shader%20Language-blueviolet?style=for-the-badge&logo=opengl&logoColor=white) ![Modular](https://img.shields.io/badge/Architecture-Modular-yellow?style=for-the-badge&logo=google-keep&logoColor=white) ![Audio](https://img.shields.io/badge/System-Audio%20Manager-red?style=for-the-badge&logo=audio-technica&logoColor=white)
# Silent Universe Project Overview

**Silent Universe** is a narrative horror game built with the Unity Engine, focusing on a chilling atmosphere, detailed inventory management, and complex interaction mechanics between the player, the environment, and NPCs. The project is currently undergoing a major architectural refactor to implement Dependency Injection for better scalability and maintainability.

## Technologies Used

  * **Game Engine**: Unity 6 (utilizing the new Render Graph API).
  * **Render Pipeline**: Universal Render Pipeline (URP).
  * **Architecture & DI**: [VContainer](https://vcontainer.hadashikick.jp/) (Dependency Injection for Unity).
  * **Input System**: Unity Input System Package (Action-based).
  * **Rendering Techniques**: Custom URP Renderer Features using Render Graph (e.g., CRT Effect).
  * **UI System**: Unity UI (uGUI) with an Event-based architecture.

## Architecture & Design Principles

The project has migrated from traditional Singleton patterns toward **Inversion of Control (IoC)** using **VContainer**.

### 1\. Composition Root (Lifetime Scopes)

The architecture is divided into scopes to manage object lifecycles:

  * **ProjectLifetimeScope**: Manages global services (Singletons) that persist across scenes, such as `SanitySystem`, `NoiseTracker`, and `QuestManager`.
  * **SceneLifetimeScope**: Manages components specific to the gameplay scene, such as `EnemyAI`, `PlayerInventory`, and UI systems.

### 2\. Dependency Injection (DI)

Components no longer search for references independently. Instead, dependencies are injected via:

  * **Field/Method Injection**: For `MonoBehaviour` components.
  * **RegisterComponent**: To register existing scene instances into the container.

### 3\. Decoupling with Interfaces

Interfaces like `IAudioManager`, `IInteractable`, and `IPersistable` ensure that Assembly Definitions (asmdef) remain loosely coupled. For example, the inventory system can trigger audio without a direct dependency on the `AudioManager` implementation.

## Design Patterns

  * **Observer Pattern**: Extensively used via `UnityEvent` for inter-system communication (e.g., `onKeyAdded` in `PlayerInventory`).
  * **Persistence Pattern**: Utilizes the `IPersistable` interface, where each system formats its own data before being saved by the `GameSaveService`.
  * **Strategy Pattern**: Implemented in the interaction system (`IInteractable`), allowing diverse objects (doors, items, NPCs) to have unique interaction behaviors called through a uniform interface.
  * **Command Pattern**: Handled via the Unity Input System for asynchronous player input.
  * **State Management**: Uses a `GameState` class to track global statuses, such as whether the CCTV mode is active.

## File & Folder Structure

The project uses **Assembly Definitions (.asmdef)** to optimize compilation times and enforce code boundaries:

```text
_Scripts/
├── _Core/                # Core systems, Interfaces (IAudioManager, IPersistable), GameSave
├── LifetimeScopes/       # VContainer configurations (Project & Scene Scopes)
├── InventorySystem/      # Player Inventory, Item logic, Pickups, and related UI
├── GameSystems/          # Main managers (AudioManager, GameManager, EnemyAI)
├── Narrative/            # DialogueManager, QuestSystem, NPCInteraction
├── Rendering/            # Custom Shaders, CRT Renderer Feature (Render Graph)
└── MainMenu/             # UI logic and visual effects for the main menu
```

## Rendering System (Custom CRT)

The game features a unique **CRT (Cathode Ray Tube)** visual effect implemented with the Unity 6 Render Graph:

1.  **RecordRenderGraph**: Captures the current camera output.
2.  **Blit Pass**: Injects the CRT material into a temporary texture.
3.  **Copy Back Pass**: Returns the processed image to the main camera target.

## Persistence System

The save system is decentralized. Through `IPersistable`, components like `PlayerInventory` are responsible for their own data state. The `GameSaveService` triggers `Persist()` on all registered implementors before writing the final `SaveFile` to disk.
