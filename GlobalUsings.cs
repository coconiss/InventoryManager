// Global usings and aliases to disambiguate WPF and WinForms types
// and to provide common namespaces across the project.

// Ensure `UserControl` refers to the WPF control (System.Windows.Controls.UserControl)
global using UserControl = System.Windows.Controls.UserControl;

// Ensure KeyEventArgs refers to WPF input event args
global using KeyEventArgs = System.Windows.Input.KeyEventArgs;

// Common IO types used across services
global using System.IO;
