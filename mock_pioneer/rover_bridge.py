import socket
import threading
import argparse
import time

def send_to_esp32(esp_ip, esp_port, cmd_str):
    # This is a placeholder for the actual ESP32 connection.
    # We will connect and send the command.
    try:
        s = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
        s.settimeout(1.0)
        s.connect((esp_ip, esp_port))
        s.sendall(cmd_str.encode('ascii'))
        s.close()
    except Exception as e:
        print(f"[Bridge] Failed to send to ESP32: {e}")

def handle_client(conn, addr, esp_ip, esp_port):
    print(f"[Bridge] Connected to server.py at {addr}")
    try:
        while True:
            data = conn.recv(1024)
            if not data:
                break
            
            lines = data.decode('ascii').splitlines()
            for line in lines:
                if not line:
                    continue
                # Line format from server.py: $cTTTSSS (e.g. $1090256)
                print(f"[Bridge] Received from mock_pioneer: {line}")
                
                # Here we could translate it to the ESP32's specific motor format.
                # For now, we forward the raw string or a basic translation.
                # Example: CMD1090256
                translated_cmd = line.replace("$", "CMD") + "\n"
                
                if esp_ip:
                    send_to_esp32(esp_ip, esp_port, translated_cmd)
                else:
                    print(f"[Bridge] (Dry Run) Would send to ESP32: {translated_cmd.strip()}")
                
    except Exception as e:
        print(f"[Bridge] Connection error: {e}")
    finally:
        conn.close()

def main():
    parser = argparse.ArgumentParser(description="Bridge between mock_pioneer and the real ESP32 rover.")
    parser.add_argument("--port", type=int, default=1238, help="Port to listen for mock_pioneer")
    parser.add_argument("--esp-ip", type=str, default="", help="IP address of the ESP32 (leave blank for dry run)")
    parser.add_argument("--esp-port", type=int, default=80, help="TCP port of the ESP32")
    args = parser.parse_args()
    
    server_sock = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
    server_sock.bind(("0.0.0.0", args.port))
    server_sock.listen(1)
    
    print(f"[Bridge] Listening for mock_pioneer on port {args.port}...")
    if not args.esp_ip:
        print("[Bridge] No ESP32 IP provided. Running in dry-run mode (will only print commands).")
    else:
        print(f"[Bridge] Will forward commands to ESP32 at {args.esp_ip}:{args.esp_port}")
        
    try:
        while True:
            conn, addr = server_sock.accept()
            t = threading.Thread(target=handle_client, args=(conn, addr, args.esp_ip, args.esp_port), daemon=True)
            t.start()
    except KeyboardInterrupt:
        print("\n[Bridge] Shutting down.")
    finally:
        server_sock.close()

if __name__ == "__main__":
    main()
