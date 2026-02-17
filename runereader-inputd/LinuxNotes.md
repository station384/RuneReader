
---
* Create a group for access
```bash
sudo groupadd -f runereader
sudo usermod -aG runereader "$USER"
newgrp runereader
```

---
* Install the daemon binary
```bash
sudo install -Dm755 /path/to/runereader-inputd /usr/local/sbin/runereader-inputd
```
---
* modify or install the runereader-inputd.socket service
```ini
[Unit]
Description=RuneReader input daemon socket

[Socket]
ListenStream=/run/runereader-inputd.sock
SocketMode=0660
SocketUser=root
SocketGroup=runereader
RemoveOnStop=true

# Optional: reduce latency
NoDelay=true

[Install]
WantedBy=sockets.target
```


* Modify or install the runereader-inputd.service service
```ini
[Unit]
Description=RuneReader input daemon (evdev monitor + uinput injection)
Requires=runereader-inputd.socket
After=systemd-udevd.service

[Service]
Type=simple

# Socket activation: systemd passes the accepted socket as fd 3
ExecStart=/usr/local/sbin/runereader-inputd --systemd-socket

# Run as root to access /dev/input/event* and /dev/uinput
User=root
Group=root

# Hardening (safe-ish for a daemon that must touch input devices)
NoNewPrivileges=true
PrivateTmp=true
ProtectSystem=strict
ProtectHome=true
ProtectKernelTunables=true
ProtectKernelLogs=true
ProtectControlGroups=true
RestrictSUIDSGID=true
LockPersonality=true
MemoryDenyWriteExecute=true

# Allow what we actually need:
# - /dev/uinput
# - /dev/input/event*
# - /run/runereader-inputd.sock (socket is handled by systemd)
DeviceAllow=/dev/uinput rw
DeviceAllow=/dev/input/event* r

# For systemd hardening when using ProtectSystem=strict:
ReadWritePaths=/dev /run

# Restart behavior
Restart=on-failure
RestartSec=0.2

# Logging
StandardOutput=journal
StandardError=journal

[Install]
WantedBy=multi-user.target
```

---
* Enable and start the service
```bash
sudo systemctl daemon-reload
sudo systemctl enable --now runereader-inputd.socket
sudo systemctl start runereader-inputd.socket
```

---
* Verify service is started
```base 
systemctl status runereader-inputd.socket
systemctl status runereader-inputd.service
```
This will show the status of the services

Optionally you can check the system journal
```bash
journalctl -u runereader-inputd.service -f
```

--- Verify permission
```bash
ls -l /run/runereader-inputd.sock
# should be: srw-rw---- root runereader ...

getent group runereader
```
That path will exist when the socket unit is enabled (even before the daemon is started), it is created by the runereader-inputd.socket
If you do not see output or permissions are incorrect, something is wrong.

