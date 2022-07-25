--- the net module (lua socket)
---@author: Wynn Yo 2022-06-24 10:18:31
local G = GetMonitordClientEnv()
local socket = G.require("lua/socket")

---@class net
local net = {}
local _net_servers
local _net_clients

-- net.listen(address, port, onConnect, backlog)

--- the interface to listen a host and port
---@param address string @the host to listen
---@param port number @the port to listen
---@param onConnect fun(cnet:net):void @the callback when a client connect
---@param backlog number @the backlog of the listen
---@return net @the listen net
function net.listen(address, port, onConnect, backlog)
    assert(address, "[net]address is nil")
    assert(port, "[net]port is nil")
    backlog = backlog or 128
    local self = net.new()
    self.address = address
    self.port = port
    self.onConnect = onConnect
    local stream = self.stream
    stream:setoption("reuseaddr", true)
    stream:bind(address, port)
    stream:listen(backlog)
    stream:settimeout(0)
    _net_servers[self] = true
    return self
end

-- net.connect(address, port, onConnect)

--- the interface to connect a host and port
---@param address string @the host to connect
---@param port number @the port to connect
---@param onConnect fun(cnet:net):void @the callback when a client connect
---@return net @the remote net
function net.connect(address, port, onConnect)
    assert(address, "[net]address is nil")
    assert(port, "[net]port is nil")
    local self = net.new()
    self.address = address
    self.port = port
    self.onConnect = onConnect
    local stream = self.stream
    local ok, err = stream:connect(address, port)
    if not ok then
        net.close(self, "stream connect error:" .. err)
        return
    end
    net.recving(self, true)
    if self.onConnect then
        self:onConnect()
    end
    _net_clients[self] = true
    return self
end

function net.close(self, reason)
    if not self then
        return
    end
    net.recving(self, false)
    local key = self.key or "unknown"
    local mark = self.mark or "unknown"
    local stream = self.stream
    if stream then
        stream:close()
        _net_clients[self] = nil
        _net_servers[self] = nil
    end
    print(debug.traceback("[net]" .. key .. "(" .. mark .. ") closed: " .. tostring(reason)))
end

-- net.update()

--- update net
function net.update()
    for snet in pairs(_net_servers) do
        local stream = snet.stream
        if stream then
            local ok, err = stream:settimeout(0)
            if not ok then
                net.close(snet, "stream settimeout error:" .. err)
            end
            local client = stream:accept()
            if client then
                local cnet = net.new()
                cnet.stream = client
                cnet.onConnect = snet.onConnect
                net.recving(cnet, true)
                _net_clients[cnet] = true
                if cnet.onConnect then
                    cnet:onConnect()
                end
            end
        end
    end

    local recvt = {}
    for cnet in pairs(_net_clients) do
        local stream = cnet.stream
        if stream then
            recvt[#recvt + 1] = stream
        end
    end
    local recvt, sendt, err = socket.select(recvt, nil, 0)
    if err and err ~= "timeout" then
        print(debug.traceback("[net]select error:" .. err))
        return
    end
    for _, stream in ipairs(recvt) do
        local cnet = nil
        for cnet_ in pairs(_net_clients) do
            if cnet_.stream == stream then
                cnet = cnet_
                break
            end
        end
        local recver = cnet and cnet._msgrecver
        if recver then
            coroutine.resume(recver)
        end
    end
end

-- net:send(message)

--- send a message to the remote
---@param message string @the message to send
function net:send(message)
    assert(message, "[net]data is nil")
    local stream = self.stream
    assert(stream, "[net]doesn't set socket stream")
    local sendingLength = message:len()
    local a, b, c, d = math.floor(sendingLength / 16777216), math.floor(sendingLength / 65536) % 256,
        math.floor(sendingLength / 256) % 256, sendingLength % 256
    local ok, err = stream:send(string.char(a, b, c, d))
    if not ok then
        net.close(self, "[net]send length error:" .. err)
    end
    ok, err = stream:send(message)
    if not ok then
        net.close(self, "[net]send data error:" .. err)
    end
end

-- net:onRecv(message)

--- the callback function of recv (override by `net.msghandler`)
---@field onRecv fun(cnet:net, data:string):void @the callback function of recv
---@param message string @the message received
function net:onRecv(message)
    print(self, message)
end

function net.peerinfo(self)
    local host, port, family = self.stream:getpeername()
    local peerinfo = {
        ip = host,
        port = port,
        family = family
    }
    return peerinfo
end

net._metatable = {
    __index = {
        type = "socket",
        send = net.send,
        onRecv = net.onRecv,
        close = net.close,
        peerinfo = net.peerinfo,
        onConnect = nil,
        onClose = nil,
    }
}

function net.new()
    local self = {}
    self.stream = (socket.tcp or socket.tcp4)()
    self.address = nil
    self.port = nil
    self.key = nil
    self.name = nil
    self.mark = nil
    setmetatable(self, net._metatable)
    return self
end

function net.msgrecver(self)
    local stream = self.stream
    assert(stream, "[net]doesn't set socket stream")
    local recvingLength = nil
    return function()
        while true do
            local ok, err = stream:settimeout(0)
            if not ok then
                net.close(self, "[net]settimeout error:" .. err)
                return
            end
            if recvingLength == nil then
                local data, err = stream:receive(4)
                if not data then
                    if err == "timeout" then
                        coroutine.yield()
                    else
                        net.close(self, "[net]receive length error:" .. err)
                        return
                    end
                else
                    local a, b, c, d = string.byte(data, 1, 4)
                    recvingLength = a * 16777216 + b * 65536 + c * 256 + d
                end
            else
                local data, err = stream:receive(recvingLength)
                if not data then
                    if err == "timeout" then
                        coroutine.yield()
                    else
                        net.close(self, "[net]receive data error:" .. err)
                        return
                    end
                else
                    if self.onRecv then
                        local ok, err = xpcall(self.onRecv, net.recverrh, self, data)
                        if not ok then
                            print(err)
                        end
                    end
                    recvingLength = nil
                end
            end
        end
    end
end

function net.recving(self, enable)
    local stream = self.stream
    if not stream then
        net.close(self, "non-stream")
        return
    end
    if enable then
        self._msgrecver = coroutine.create(net.msgrecver(self))
    else
        self._msgrecver = nil
    end
end

function net.recverrh(err)
    return debug.traceback("[net]recver error: " .. tostring(err), 2)
end

local function module_initializer()
    _net_servers = {}
    _net_clients = {}

    local module = {
        connect = net.connect,
        listen = net.listen,
        close = net.close,
        update = net.update
    }
    return module
end

return module_initializer()
