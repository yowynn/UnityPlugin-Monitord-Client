--- luaremote client agent
---@author: Wynn Yo 2022-06-22 15:06:35
local G = GetMonitordClientEnv()
local net = G.require("lua/socketnet")
local event = G.require("lua/event")

local client = {}
local _client_server
local _client_env
function client.connect(address, port, onConnect, onClose)
    print("[lua-remote]Start Client~ connect " .. address .. ":" .. port)
    local server = net.connect(address, port, function(server)
        server.onClose = function(server, reason)
            print("[lua-remote]Client disconnected")
            _client_server = nil
            if onClose then
                onClose(reason)
            end
        end
        event.reg(server)
        _client_server = server
        if onConnect then
            onConnect(server)
        end
    end)
end

function client.setenv(env)
    _client_env = env
end

function client.getenv()
    return _client_env
end

function client.server()
    if _client_server then
        return event().to(_client_server)
    else
        return event.NOCALL
    end
end

function client.update()
    net.update()
end

local function module_initializer()
    _client_server = nil
    _client_env = nil

    local module = {
        connect = client.connect,
        setenv = client.setenv,
        getenv = client.getenv,
        server = client.server,
        update = client.update,
    }
    return module
end

return module_initializer()
