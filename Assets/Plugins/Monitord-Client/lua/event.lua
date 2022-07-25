--- the net event module
---@author: Wynn Yo 2022-06-13 10:15:19
local G = GetMonitordClientEnv()
local timer = G.require("lua/timer")
local dump = G.require("lua/table_dump")

---@class event @the net event
local event = {}
local _event_registeredEvents
local _event_registeredHandlers
local _event_from_handler

function event.reg(handler, send)
    send = send or event.sender(handler, true)
    local recv = event.recver(handler, true)
    _event_registeredHandlers[handler] = {send = send, recv = recv}
    return recv
end

function event.unreg(handler)
    _event_registeredHandlers[handler] = nil
end

function event.sender(handler, new)
    local handlers = _event_registeredHandlers[handler]
    if handlers and handlers.send then
        return handlers.send
    end
    if new then
        return function(eventName, ...)
            if not _event_registeredHandlers[handler] then
                print("[E][event]handler not registered")
                return
            end
            local callargs = table.pack(eventName, ...)
            local message = event.encodelua(callargs)
            if (handler.send) then
                handler:send(message)
            else
                print("[E][event]handler has no \"send\" method")
            end
        end
    end
end

function event.recver(handler, new)
    local handlers = _event_registeredHandlers[handler]
    if handlers and handlers.recv then
        return handlers.recv
    end
    if new then
        return function(eventName, ...)
            if not _event_registeredHandlers[handler] then
                print("[E][event]handler not registered")
                return
            end
            return event.invoke(handler, eventName, ...)
        end
    end
end

function event.invoke(from, eventName, ...)
    local stackfrom = event.setfrom(from)
    local ok, ret = event.pcall(eventName, ...)
    event.setfrom(stackfrom)
    if ok then
        return ret
    else
        return nil, ret
    end
end

function event.pcall(eventName, ...)
    local func
    if (type(eventName) == "string") then
        func = _event_registeredEvents[eventName]
    elseif (type(eventName) == "function") then
        func = eventName
    end
    local ok, ret
    if func then
        ok, ret = pcall(func, ...)
    else
        ok = false
        ret = "Event not found: " .. tostring(eventName)
    end
    return ok, ret
end

function event.from()
    return _event_from_handler
end

function event.setfrom(from)
    local old = _event_from_handler
    _event_from_handler = from
    return old
end

function event.encodelua(callargs)
    local message = dump(callargs, false)
    return message
end

function event.decodelua(message)
    local ok, ret = pcall(function()
        return load("return " .. message)()
    end)
    if ok then
        return ret
    else
        print("[E][event]decode message fail: " .. tostring(message))
        return nil
    end
end

function event.buildcontext()
    local context = {}
    local ctx_from, ctx_handler, ctx_delay, ctx_rep
    local clearctx = function()
        ctx_from = nil
        ctx_handler = nil
        ctx_delay = nil
        ctx_rep = nil
    end

    function context.from(handler)
        ctx_from = handler
        return context
    end

    function context.to(handler_s, isMulticast)
        if isMulticast then
            ctx_handler = function(eventName, ...)
                for _, handler in pairs(handler_s) do
                    local send = event.sender(handler)
                    if send then
                        send(eventName, ...)
                    end
                end
            end
        else
            ctx_handler = function(eventName, ...)
                local send = event.sender(handler_s)
                if send then
                    send(eventName, ...)
                end
            end
        end
        return context
    end

    function context.delay(delay_s, rep_s)
        ctx_delay = (delay_s and delay_s > 0 and delay_s or 0) * 1000
        ctx_rep = (rep_s and rep_s > 0 and rep_s or 0) * 1000
        return context
    end

    setmetatable(context, {
        __index = function(t, k)
            local v = function(...)
                local delay, rep = ctx_delay, ctx_rep
                local from = ctx_from or event.from()
                local handler = ctx_handler or event.recver(from)
                clearctx()
                if delay then
                    local callargs = table.pack(k, ...)
                    local t = timer.start(delay, rep, function()
                        local data = handler(table.unpack(callargs, 1, callargs.n))
                        return data
                    end)
                    return t
                else
                    local data = handler(k, ...)
                    return data
                end
            end
            rawset(t, k, v)
            return v
        end,
        __newindex = function(t, k, v)
            error("[E][event]context is readonly")
        end
    })
    return function()
        return context
    end
end

local function module_initializer()
    _event_from_handler = nil
    _event_registeredHandlers = setmetatable({}, {__mode = "k"})
    _event_registeredEvents = {
        print = print, -- for test
        call = event.pcall -- for test
    }

    local NOCALL_FUNC = function()
    end

    local NOCALL = setmetatable({}, {
        __index = function(t, k)
            return NOCALL_FUNC
        end,
        __newindex = function(t, k, v)
            error("[E][event]NOCALL is readonly")
        end
    })

    local module = {
        reg = event.reg,
        unreg = event.unreg,
        from = event.from,
        decode = event.decodelua,
        NOCALL = NOCALL
    }
    setmetatable(module, {
        __call = event.buildcontext(),
        __index = _event_registeredEvents,
        __newindex = _event_registeredEvents
    })
    return module
end

return module_initializer()
