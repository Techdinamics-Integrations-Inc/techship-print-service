FROM debian:bookworm-slim

ENV DEBIAN_FRONTEND=noninteractive

RUN apt-get update && apt-get install -y --no-install-recommends \
    bash \
    dos2unix \
    cups-ipp-utils \
    ghostscript \
    inotify-tools \
    dbus \
    avahi-daemon \
    avahi-utils \
    && rm -rf /var/lib/apt/lists/*

RUN mkdir -p /spool /output /var/run/dbus && chmod 0777 /spool /output

RUN sed -i 's/^#enable-dbus=.*/enable-dbus=yes/' /etc/avahi/avahi-daemon.conf && \
    sed -i 's/^#use-ipv6=.*/use-ipv6=no/' /etc/avahi/avahi-daemon.conf && \
    sed -i 's/^#allow-interfaces=.*/allow-interfaces=eth0/' /etc/avahi/avahi-daemon.conf || true

EXPOSE 58150

COPY entrypoint.sh /entrypoint.sh
RUN dos2unix /entrypoint.sh && chmod +x /entrypoint.sh

CMD ["bash", "/entrypoint.sh"]
