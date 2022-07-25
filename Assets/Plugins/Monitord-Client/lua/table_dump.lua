--- dump table to string
---@author: Wynn Yo 2022-06-02 10:26:53
local INDENT
local NEWLINE
local FOLDING_TAG
local VERBOSE
local DEPTH_LIMIT

local _curr_verbose = nil
local _curr_depth_limit = nil

local dumptable, dumptablekey, dumptablevalue, dumpstring, dumpnumber, dumpboolean, dumporiginal, dumpall

function dumptable(t, depth)
    depth = (depth or 0) + 1
    local s = "{"
    if _curr_verbose then
        s = s .. " " .. dumporiginal(t, false)
    end
    s = s .. NEWLINE
    local vkpairs = {}
    for k, v in pairs(t) do
        vkpairs[#vkpairs + 1] = INDENT:rep(depth) .. dumptablekey(k, depth) .. " = " .. dumptablevalue(v, depth)
    end
    table.sort(vkpairs)
    for _, v in ipairs(vkpairs) do
        s = s .. v
        if not _curr_verbose then
            s = s .. ","
        end
        s = s .. NEWLINE
    end
    if _curr_verbose then
        local mt = getmetatable(t)
        if mt then
            s = s .. INDENT:rep(depth) .. ".metatable = " .. dumptable(mt, depth) .. NEWLINE
        end
    end
    s = s .. INDENT:rep(depth - 1) .. "}"
    return s
end

function dumptablekey(t, depth)
    local s = dumpall(t, depth)
    return "[" .. s .. "]"
end

function dumptablevalue(t, depth)
    local s = dumpall(t, depth)
    return s
end

function dumpstring(t, depth)
    local s
    if _curr_verbose then
        s = "\"" .. t .. "\""
    else
        s = string.format("%q", t)
    end
    return s
end

function dumpnumber(t, depth)
    local s = tostring(t)
    return s
end

function dumpboolean(t, depth)
    local s = tostring(t)
    return s
end

function dumporiginal(t, fold)
    fold = fold and FOLDING_TAG or ""
    if _curr_verbose then
        return "<" .. tostring(t) .. fold .. ">"
    else
        return "\"DMPERR:" .. tostring(t) .. fold .. "\""
        -- return "nil" .. INDENT .. "--[[" .. tostring(t) .. fold .. "]]"
    end
end

function dumpall(t, depth)
    if depth >= _curr_depth_limit then
        return dumporiginal(t, true)
    end
    if type(t) == "table" then
        return dumptable(t, depth)
    elseif type(t) == "string" then
        return dumpstring(t, depth)
    elseif type(t) == "number" then
        return dumpnumber(t, depth)
    elseif type(t) == "boolean" then
        return dumpboolean(t, depth)
    else
        return dumporiginal(t, false)
    end
end

--- Dump a table to a string.
---@param t table|any @table / target to dump
---@param verbose boolean @if true, dump the additional information, such as metatable, etc.
---@param depth number @depth limit
---@return string
local function dump(t, verbose, depth)
    _curr_verbose = verbose == nil and VERBOSE or verbose
    _curr_depth_limit = depth == nil and DEPTH_LIMIT or depth
    local s = dumpall(t, 0)
    return s
end

local function module_initializer()
    INDENT = "\t"
    NEWLINE = "\n"
    FOLDING_TAG = "..."
    VERBOSE = true
    DEPTH_LIMIT = math.huge

    _curr_verbose = nil
    _curr_depth_limit = nil

    if table and not table.dump then
        table.dump = dump
    end

    return dump
end

return module_initializer()
