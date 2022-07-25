--- the timer module
---@author: Wynn Yo 2022-06-22 14:53:35
local uv = require("luv")

---@class timer
local timer = {}

function timer.start(timeout, interval, callback, ...)
    local callargs = table.pack(...)
    local t = {}
    local function wrapcallback()
        callback(table.unpack(callargs, 1, callargs.n))
        if interval then
            t.timer = Timer.call(interval, function()
                wrapcallback()
            end)
        end
    end
    t.timer = Timer.call(timeout, wrapcallback)
    return t
end

function timer.stop(t)
    Timer.stop(t and t.timer)
end

function timer.update()
    -- Timer.update()
end

local function module_initializer()
    local module = {
        start = timer.start,
        stop = timer.stop,
        update = timer.update
    }
    return module
end

return module_initializer()
