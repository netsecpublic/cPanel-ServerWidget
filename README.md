# cPanel Server Status Monitor (ServerWidget)

A lightweight, borderless, always-on-top desktop widget built with C# WPF for Windows. It provides real-time monitoring of cPanel/WHM server metrics—specifically system load averages and Exim mail queue counts—with customizable alarm thresholds, audio alerts, and system tray integration.

![ServerWidget Preview](https://img.shields.io/badge/Platform-Windows-blue) ![Framework-.NET 8](https://img.shields.io/badge/.NET-8.0-purple) ![License-MIT](https://img.shields.io/badge/License-MIT-green)

---

## Features

* **Real-Time Monitoring:** Polls your servers on a 30-second interval for current server load and mail queue counts.
* **Color-Coded Status Bars:** 
  * 🟢 **Green:** Server is online and all metrics are healthy.
  * 🟠 **Orange:** High load or mail queue threshold breached (or muted).
  * 🔴 **Red:** Server is offline/unreachable.
* **Custom Alarm Audio:** Plays a designated `.wav` file or system beep when an alarm triggers.
* **Flexible Muting:** Right-click any server bar to temporarily mute alarms (1 min up to 24 hours, or indefinitely).
* **System Tray Integration:** Runs discreetly in your system tray with a context menu to manage settings, restore the window, or quit.
* **Secure Endpoint:** Uses an `X-Auth-Token` header with timing-attack-safe validation (`hash_equals`) to protect your server script.
* **Lightweight Footprint:** Framework-dependent single-file publishing keeps the executable tiny.

---

## Server-Side Setup (`queue.php`)

To feed data to the widget securely with zero disk write overhead, deploy the provided `queue.php` script to your servers.

### 1. Upload the Script
Upload `queue.php` to a web-accessible directory on your server (e.g., `https://yourserver.com/queue.php` or a secure subdirectory).

### 2. Configure Authentication
Open `queue.php` and set your secret token:
```php
$secret_token = "YOURPASSWORD123"; // Must match your widget's Auth Token
```
###  3. Cron Job Configuration (Required for Exim Mail Queue if shell_exec is blocked)
If your server's PHP configuration blocks shell_exec(), the script reads a local queue_count.txt fallback file.

Note: The queue_count.txt file contains only the raw integer mail queue count.

Root Permission Required: The cron job must be installed as ROOT (via root SSH crontab), because standard user accounts do not have the system permissions required to query /usr/sbin/exim -bpc.

To set up the root cron job (runs every minute):

Log into your server via SSH as root.

Open the root crontab editor:

Bash

_crontab -e_

Add a cron job to update the queue count every minute:

Bash 
```
* * * * * /usr/sbin/exim -bpc > /home/username/public_html/queue_count.txt 2>&1
```
(Adjust the absolute path to point to your script's directory).


And here is **Part 2** (Installation, Compilation, and Usage):

## Installation & Compilation

### Requirements

* **.NET 8.0 Desktop Runtime (Windows x64):** Required on any *target machine* running the compiled executable.


How to Use
First Launch: When opened with no servers configured, the widget displays an amber placeholder bar reading "Right-click to add".

Adding Servers:

Right-click anywhere on the widget background (or right-click the system tray icon) and select Settings.

Click Add New Server.

Fill out your Server Name (e.g., cp3), the Script URL (https://server.com/queue.php), and your Script Auth Token (YOURPASSWORD123).

Context Menu Controls:

Right-click a server bar to open its specific context menu.

Mute Alarms: Choose a duration to silence specific server warnings.

Minimize to Tray: Hides the desktop overlay while keeping monitoring active in the background.

License
This project is open-source and available under the MIT License.
