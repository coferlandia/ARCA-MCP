import sys, json, urllib.request

token = sys.argv[1]
method = sys.argv[2]
params = json.loads(sys.argv[3]) if len(sys.argv) > 3 else None

payload = {"jsonrpc": "2.0", "id": 1, "method": method}
if params is not None:
    payload["params"] = params

req = urllib.request.Request(
    "http://localhost:5299/",
    data=json.dumps(payload).encode(),
    headers={
        "Content-Type": "application/json",
        "Accept": "application/json, text/event-stream",
        "Authorization": f"Bearer {token}",
    },
)
try:
    with urllib.request.urlopen(req) as resp:
        print("STATUS:", resp.status)
        print(resp.read().decode())
except urllib.error.HTTPError as e:
    print("STATUS:", e.code)
    print(e.read().decode())
