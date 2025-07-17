# ClickSpy - Advanced Mouse Click Logger & Screenshot Utility

!

ClickSpy is a lightweight, high-performance background utility for Windows that silently logs left mouse clicks and captures a corresponding screenshot for each event. It is designed to be efficient and unobtrusive, running entirely from the system tray.

Built with .NET 8, it correctly handles multi-monitor setups and provides detailed logs, making it a powerful tool for user activity analysis, diagnostics, or personal record-keeping.

---

## Features

-   **Silent Background Operation**: Runs discreetly in the system tray with no visible windows.
-   **System-Wide Click Logging**: Captures left mouse clicks across all applications and monitors.
-   **Dual Capture Modes**: On-the-fly switching between capturing:
    -   The **entire monitor** where the click occurred.
    -   The **active window** that was clicked.
-   **Multi-Monitor Support**: Accurately detects coordinates and captures the correct screen in multi-display environments.
-   **Detailed Event Logging**: Creates a `log.txt` file with comprehensive information for each click, including:
    -   Precise timestamp
    -   Global screen coordinates
    -   The name of the process that was clicked (e.g., `chrome.exe`)
    -   The capture target (monitor ID or window title)
    -   A reference to the saved screenshot file.
-   **High Performance**: Uses an asynchronous, queue-based architecture to ensure the logging process never impacts system performance.
-   **Easy to Use**: Controlled via a simple right-click menu on the system tray icon.

---

## Requirements

-   **Operating System**: Windows 10 / 11
-   **Framework**: .NET 8 SDK
-   **IDE (Recommended)**: Visual Studio 2022

---

## Setup and Installation

1.  **Get the Code**: Clone this repository or download the source code.

2.  **Open in Visual Studio**: Open the project solution file (`.sln`) in Visual Studio 2022.

3.  **Install Dependencies**: The project requires the `System.Drawing.Common` library (on some systems). To install it:
    -   Right-click the project in the Solution Explorer.
    -   Select "Manage NuGet Packages...".
    -   Search for `System.Drawing.Common` and install it.

4.  **(Optional) Add a Custom Icon**:
    -   Add your `.ico` file to the project.
    -   In the file's properties, set **Build Action** to **Embedded Resource**.
    -   In the `TrayApplicationContext.cs` file, update the `NotifyIcon` initialization to load your icon:
        ```csharp
        // Replace this line:
        Icon = new Icon(System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream("ClickSpy.spy-icon.ico")), // Use a default icon
        
        // With this line (update names accordingly):
        Icon = new Icon(System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream("YourProjectName.YourIconName.ico")),
        ```

5.  **Build the Project**:
    -   From the top menu, select `Build` -> `Build Solution`.
    -   The executable file will be created in the `bin/Debug` or `bin/Release` folder.

---

## How to Use

1.  **Run the Application**: Double-click the generated `.exe` file. The application will start silently, and you will see its icon appear in the system tray.

2.  **Control the App**: Right-click the tray icon to open the context menu. From here you can:
    -   **Switch Capture Mode**: Select either "Tryb: Monitor" (Monitor Mode) or "Tryb: Okno" (Window Mode). The checkmark indicates the active mode.
    -   **Exit**: Safely close the application. This will stop all logging and remove the tray icon.

3.  **Check the Output**:
    -   **Screenshots**: All captured images are saved as `.png` files in the `screenshots` folder, located in the same directory as the executable.
    -   **Logs**: All event details are appended to the `log.txt` file, also in the application's main directory.

---

## How It Works

ClickSpy's efficiency comes from its smart architecture:

-   A **low-level global mouse hook** (`SetWindowsHookEx`) listens for click events without consuming significant resources.
-   When a click is detected, the hook's callback function performs only one, lightning-fast action: it adds the click's coordinates to a thread-safe queue.
-   A separate **background task** (the "consumer") constantly monitors this queue. When new data appears, it performs the "heavy" work: identifying the target window/monitor, taking the screenshot, and writing the log entry.

This **producer-consumer pattern** ensures that the system's input processing is never delayed, resulting in a smooth user experience.

---

## License

This project is licensed under the MIT License. See the `LICENSE` file for details.

