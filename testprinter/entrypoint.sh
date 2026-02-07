#!/usr/bin/env bash
set -e

mkdir -p /var/run/dbus /spool /output
chmod 0777 /spool /output || true

# Start system dbus (needed for Avahi)
dbus-daemon --system --fork

# Start Avahi (DNS-SD provider)
avahi-daemon --no-drop-root --daemonize

# Start IPP printer:
# -p 58150 = port
# -d /spool = spool directory
# -k = keep spool files so we can convert them
# -f = accept these MIME types (do NOT restrict to PDF only)
# final arg is required printer name
ippeveprinter -v -p 58150 -d /spool -k \
  -f application/octet-stream,application/pdf,application/postscript,text/plain,\
application/vnd.cups-pdf,application/vnd.cups-postscript,application/vnd.cups-raster,\
application/vnd.cups-banner,application/vnd.cups-pdf-banner,\
image/pwg-raster,image/urf,application/PCLm \
  "PDF_Printer" &


# Convert incoming spooled jobs to PDFs in /output (mapped to D:\vprinter)
while true; do
  inotifywait -e close_write,moved_to,create /spool >/dev/null 2>&1 || true
  for f in /spool/*; do
    [ -f "$f" ] || continue
    out="/output/$(basename "$f").pdf"

    echo "spool: $f"
    head -c 16 "$f" | xxd || true
    file -b "$f" || true

    # If already PDF, copy as-is
    if head -c 5 "$f" | grep -q "%PDF-"; then
      cp "$f" "$out"
    else
      # Best-effort convert (text/ps/etc.)
      gs -q -dBATCH -dNOPAUSE -sDEVICE=pdfwrite -sOutputFile="$out" "$f" || true
    fi

    rm -f "$f"
  done
done
