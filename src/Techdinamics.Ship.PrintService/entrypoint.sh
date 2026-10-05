#!/bin/bash

# CUPS modes:
#   built-in (default) - start CUPS inside the container and print to its queues
#   remote             - CUPS_SERVER is set (host:port or socket path); lp sends jobs there
#                        and no local CUPS, Avahi or DBus is started

start_builtin_cups() {
    # Start DBUS
    mkdir -p /var/run/dbus
    rm -f /var/run/dbus/pid
    dbus-daemon --system --fork

    # Start Avahi for network printer discovery
    echo "Starting Avahi daemon..."
    avahi-daemon --daemonize

    # Configure CUPS at runtime to ensure it works even if /etc/cups is a volume
    echo "Configuring CUPS..."

    # Add a user for CUPS administration if environment variables are set
    CUPS_USER=${CUPS_USER:-admin}
    CUPS_PASSWORD=${CUPS_PASSWORD:-admin}

    if [ "$CUPS_PASSWORD" == "admin" ]; then
        echo "WARNING: CUPS admin password is the default. Set CUPS_PASSWORD before exposing port 631."
    fi

    if ! id "$CUPS_USER" >/dev/null 2>&1; then
        echo "Creating CUPS user: $CUPS_USER"
        useradd -m -G lpadmin -s /bin/bash "$CUPS_USER"
    fi
    echo "$CUPS_USER:$CUPS_PASSWORD" | chpasswd

    # Ensure the user is in the lpadmin group (double check)
    usermod -aG lpadmin "$CUPS_USER"

    CONF=/etc/cups/cupsd.conf

    sed -i 's/Listen localhost:631/Listen 0.0.0.0:631/' $CONF
    sed -i 's/WebInterface No/WebInterface Yes/' $CONF

    # Function to ensure a directive is in a section
    update_section() {
        local section=$1
        local line=$2
        if ! grep -q "$line" $CONF; then
            echo "Updating section $section with $line"
            # Match <Location section> and append line after it
            sed -i "\|<Location $section>|a \  $line" $CONF
        fi
    }

    # Ensure basic auth for admin and conf
    for loc in / /admin /conf; do
        if [ "$loc" == "/" ]; then
            update_section "$loc" "Allow All"
        else
            update_section "$loc" "Order allow,deny"
            update_section "$loc" "Allow all"
            update_section "$loc" "AuthType Basic"
            update_section "$loc" "Require user @lpadmin"
        fi
    done

    # Global settings
    sed -i 's/^SystemGroup .*/SystemGroup lpadmin/' $CONF
    grep -q "^SystemGroup lpadmin" $CONF || echo "SystemGroup lpadmin" >> $CONF
    grep -q "DefaultEncryption Never" $CONF || echo "DefaultEncryption Never" >> $CONF
    grep -q "ServerAlias *" $CONF || echo "ServerAlias *" >> $CONF
    grep -q "DefaultAuthType Basic" $CONF || echo "DefaultAuthType Basic" >> $CONF

    # Start CUPS service
    echo "Starting CUPS service..."
    /usr/sbin/cupsd -f &

    # Start cups-browsed to discover remote printers
    echo "Starting cups-browsed..."
    /usr/sbin/cups-browsed &

    # Wait for services to start
    sleep 2
}

if [ -n "$CUPS_SERVER" ]; then
    echo "Remote CUPS mode: printing via $CUPS_SERVER (built-in CUPS not started)"
else
    echo "Built-in CUPS mode"
    start_builtin_cups
fi

# Start the .NET application
echo "Starting Techship Print Service..."
exec dotnet Techdinamics.Ship.PrintService.dll
