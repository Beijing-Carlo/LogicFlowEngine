# LogicFlowEditor

LogicFlowEditor is a Blazor WebAssembly application for visually designing, editing, and managing logic flows using the LogicFlowEngine backend. It provides an interactive, browser-based UI for creating node-based logic graphs.

## Features

- Visual node-based editor for logic flows
- Drag-and-drop node creation and connection
- Real-time graph editing and validation
- Integration with LogicFlowEngine for execution and serialization
- Runs entirely in the browser (WebAssembly, .NET 8)

## Getting Started

1. Clone the repository and open the solution in Visual Studio 2022 or later.
2. Set `LogicFlowEditor` as the startup project.
3. Run the project (F5 or Ctrl+F5) to launch the editor in your browser.

## Project Structure

- `Components/` – Blazor components for the editor UI
- `Services/` – Application services (state management, serialization, etc.)
- `wwwroot/` – Static assets (JS, CSS, images)
- References `LogicFlowEngine` for backend logic

## Customization

You can extend the editor by adding new node types in the LogicFlowEngine project and updating the editor UI to support them.

## License

This project is licensed under the MIT License. See the [LICENSE](../LICENSE) file for details.