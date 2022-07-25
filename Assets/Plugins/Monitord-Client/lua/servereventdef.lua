local G = GetMonitordClientEnv()

local event = G.require("lua/event")
local client = G.require("lua/socket_rpc_client")

function event.RunCode(code)
    local f, err = load(code, "remote")
    if f then
        debug.setupvalue(f, 1, client.getenv() or _ENV)
        xpcall(f, function(err)
            print("[E][exec]" .. tostring(err))
        end)
    else
        print("[E][exec]" .. tostring(err))
    end
end
