--- init tool CFG
local REQUIRE_LUA_PATH = "lua/"
local MONITORD_SERVER_HOST = "dbg.ihuman.cc"
local MONITORD_SERVER_PORT = 14213
local MONITORD_SERVER_RECONNECT_INTERVAL_SECONDS = 3
local CSHARP_UNITY_NAMESPACE = Wynne.MonitordClient

local APP_KEY = "[DELAYED-ASSIGN]"
local TOKEN = "[DELAYED-ASSIGN]"
local SHOW_NAME = "[DELAYED-ASSIGN]"


--- init tool ENV
if not GetMonitordClientEnv then
    local monitordEnv = {}
    rawset(_G, "GetMonitordClientEnv", function()
        return monitordEnv
    end)

    --- require-hood
    local _localRequireMap = {}
    local function AddToRequireMap(key, module)
        _localRequireMap[key] = module
    end
    AddToRequireMap("lua/table_dump", require(REQUIRE_LUA_PATH .. "table_dump"))
    AddToRequireMap("lua/event", require(REQUIRE_LUA_PATH .. "event"))
    AddToRequireMap("lua/socket_rpc_client", require(REQUIRE_LUA_PATH .. "socket_rpc_client"))
    AddToRequireMap("lua/timer", require(REQUIRE_LUA_PATH .. "timer_custom"))
    AddToRequireMap("lua/socket", rawget(_G, "socket") or _G.require("socket"))
    AddToRequireMap("lua/servereventdef", require(REQUIRE_LUA_PATH .. "servereventdef"))
    local require = function(key)
        local module = _localRequireMap[key]
        if module then
            return module
        end
    end
    monitordEnv.require = require

    --- update adaptar
    local _updateTimer
    local function Updater(interval)
        if interval then
            Updater(false)
            interval = tonumber(interval) or 0.1
            _updateTimer = Timer.call(interval, function()
                client.update()
            end, 0, nil, true)
        else
            Timer.stop(_updateTimer)
            _updateTimer = nil
        end
    end
    monitordEnv.Updater = Updater

    --- get the main part in C#
    local function GetHolder()
        --- unity find Component
        local CSHARP_UNITY_TYPE = CSHARP_UNITY_NAMESPACE and CSHARP_UNITY_NAMESPACE.Reporter
        local component = CSHARP_UNITY_TYPE and Object and Object.FindObjectOfType(typeof(CSHARP_UNITY_TYPE))
        return component
    end
    monitordEnv.GetHolder = GetHolder

end
local G = GetMonitordClientEnv()
local Holder = G.GetHolder()
if not Holder then
    print("[E][monitord]Holder not found")
    return
else
    MONITORD_SERVER_HOST = Holder.ServerHost or MONITORD_SERVER_HOST
    APP_KEY = Holder.AppKey or APP_KEY or "unknown"
    TOKEN = Holder.DeviceKey or TOKEN or "unknown"
    SHOW_NAME = Holder.DeviceShowName or SHOW_NAME or "unknown"
end

local client = G.require("lua/socket_rpc_client")
local event = G.require("lua/event")

local _reconnectTimer
G.Reconnect = function()
    Timer.stop(_reconnectTimer)
    _reconnectTimer = Timer.call(MONITORD_SERVER_RECONNECT_INTERVAL_SECONDS, G.Connect)
end
G.Connect = function()
    client.connect(MONITORD_SERVER_HOST, MONITORD_SERVER_PORT, G.OnConnected, G.Reconnect)
    client.setenv(_ENV)
end
G.OnConnected = function()
    G.Updater(0.1)
    client.server().mark("client", TOKEN, SHOW_NAME)
    client.server().MarkApp(APP_KEY)

    local stream = Holder.CreateStream()
    local SyncCollections = function()
        local data = stream.ReadRest(true);
        local map = {logs = {}, stats = {}}
        for i = 0, data.logs.Length - 1 do
            map.logs[i] = data.logs[i]
        end
        for i = 0, data.stats.Length - 1 do
            map.stats[i] = data.stats[i]
        end
        client.server().syncollec(map)
    end
    event().delay(0, 3).call(SyncCollections)
end

G.Connect()




