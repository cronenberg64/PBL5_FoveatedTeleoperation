import socket
import threading
import argparse
import time

esp32_conn = None
esp32_lock = threading.Lock()

def esp32_listener(port):
    global esp32_conn
    server = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
    server.bind(("0.0.0.0", port))
    server.listen(1)
    print(f"[Bridge] Listening for ESP32 on port {port}...")
    
    while True:
        try:
            conn, addr = server.accept()
            print(f"[Bridge] ESP32 CONNECTED from {addr}!")
            with esp32_lock:
                if esp32_conn:
                    try: esp32_conn.close()
                    except: pass
                esp32_conn = conn
        except Exception as e:
            print(f"[Bridge] ESP32 accept error: {e}")

def handle_server_py(conn, addr):
    global esp32_conn
    print(f"[Bridge] server.py connected from {addr}")
    try:
        while True:
            data = conn.recv(1024)
            if not data:
                break
            
            lines = data.decode('ascii').splitlines()
            for line in lines:
                if not line.startswith('$') or len(line) < 8:
                    continue
                
                print(f"[Bridge] Received from server.py: {line}")
                
                try:
                    cmd_char = line[1]
                    turn = int(line[2:5])
                    speed = int(line[5:8])
                    
                    servo = int(30 + (turn / 999.0) * 120)
                    
                    # Convert Unity speed (0-512, neutral 256) to throttle magnitude
                    throttle = (speed - 256) * (200.0 / 256.0)
                    
                    if cmd_char == '1': # Forward
                        mtr = int(255 + throttle)
                    elif cmd_char == '2': # Reverse
                        mtr = int(255 - throttle)
                    else: # Stop
                        mtr = 255
                        
                    mtr = max(55, min(455, mtr)) # Clamp to ESP32 range
                    
                    translated_cmd = f"CMD{servo:03d}{mtr:03d}\n"
                    
                    with esp32_lock:
                        if esp32_conn:
                            try:
                                esp32_conn.sendall(translated_cmd.encode('ascii'))
                                print(f"[Bridge] Sent to ESP32: {translated_cmd.strip()}")
                            except Exception as e:
                                print(f"[Bridge] Lost ESP32 connection: {e}")
                                esp32_conn.close()
                                esp32_conn = None
                        else:
                            print(f"[Bridge] Waiting for ESP32 to connect... (would send {translated_cmd.strip()})")
                except ValueError:
                    pass
    except Exception as e:
        print(f"[Bridge] Connection error: {e}")
    finally:
        conn.close()

def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--listen-server", type=int, default=1238, help="Port to listen for server.py")
    parser.add_argument("--listen-esp", type=int, default=1234, help="Port to listen for ESP32")
    args = parser.parse_args()
    
    # Start ESP32 listener thread
    t = threading.Thread(target=esp32_listener, args=(args.listen_esp,), daemon=True)
    t.start()
    
    # Listen for server.py
    server_sock = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
    server_sock.bind(("0.0.0.0", args.listen_server))
    server_sock.listen(1)
    
    print(f"[Bridge] Listening for server.py on port {args.listen_server}...")
    
    try:
        while True:
            conn, addr = server_sock.accept()
            t = threading.Thread(target=handle_server_py, args=(conn, addr), daemon=True)
            t.start()
    except KeyboardInterrupt:
        print("\n[Bridge] Shutting down.")
    finally:
        server_sock.close()

if __name__ == "__main__":
    main()
