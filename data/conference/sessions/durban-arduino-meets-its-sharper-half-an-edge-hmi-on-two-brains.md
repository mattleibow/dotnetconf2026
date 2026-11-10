# Arduino Meets Its Sharper Half: An Edge HMI on Two Brains

The Arduino UNO Q is a strange and wonderful thing: a classic Arduino for precise real-time I/O, bolted to a quad-core Qualcomm processor running full Debian Linux with a GPU. Two brains on one board, and the Linux side is exactly the kind of edge compute AWS IoT services are built to talk to.

This session builds a real HMI on that board, in C#. A real Avalonia interface running on .NET on the Linux side drives a display, while the STM32 microcontroller reads sensors and handles buttons and hardware in real time. The two talk over Arduino's message-passing router, and our .NET app connects to it directly and becomes a first-class citizen on the bridge.

A reading on a pin becomes a value on screen, and a control on screen moves something real, all running on a Linux edge node small enough to sit on a workbench. If you're building for Azure IoT at the edge and want to see what a genuinely capable, general-purpose Linux brain paired with a real-time microcontroller looks like in practice, this shows you how.
