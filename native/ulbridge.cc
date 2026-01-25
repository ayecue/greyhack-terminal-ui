#define _CRT_SECURE_NO_WARNINGS 1

// =============================================================================
// Includes
// =============================================================================

// Ultralight
#include <Ultralight/Ultralight.h>
#include <AppCore/Platform.h>

// Standard Library
#include <atomic>
#include <chrono>
#include <condition_variable>
#include <filesystem>
#include <fstream>
#include <functional>
#include <iostream>
#include <mutex>
#include <queue>
#include <sstream>
#include <thread>
#include <unordered_map>
#include <variant>
#include <vector>

// UTF conversion (header-only library)
#include <utf8.h>

// JSON serialization
#include <nlohmann/json.hpp>
using json = nlohmann::json;

// Cryptographic random (libsodium)
#include <sodium.h>

// Platform I/O
#include <fcntl.h>
#include <sys/stat.h>
#include <sys/types.h>

#ifndef _MSC_VER
#include <unistd.h>
#else
#include <io.h>
#endif

// =============================================================================
// Macros
// =============================================================================

#ifdef ULBRIDGE_DEBUG
#define ULDEBUG(a) std::cerr << a << std::endl
#else
#define ULDEBUG(a) do {} while (false)
#endif

// =============================================================================
// External Events
// =============================================================================
//   0 = Command    - JS bridge command from page (name, args in JSON)
//   1 = Console    - JS console message (level, message, source, line, column)
//   2 = Cursor     - Cursor change (cursorType)
//   3 = Load       - Load events (loadEventType, frameId, isMainFrame, url, error info)
//   4 = Log        - Internal log message
//   5 = Error      - Internal error message
//   6 = ViewCreated - View was created (viewName, securityToken)

enum class ULEventType : int {
    Command = 0,
    Console = 1,
    Cursor = 2,
    Load = 3,
    Log = 4,
    Error = 5,
    ViewCreated = 6
};

// Forward declare event firing
static void fireEvent(ULEventType type, const char* viewName, const json& data);

#define ULLOG(msg) do { \
    std::ostringstream _uls; _uls << msg; \
    std::cerr << "[ULBridge] " << _uls.str() << std::endl; \
    fireEvent(ULEventType::Log, "", json{{"message", _uls.str()}}); \
} while (false)

#define ULERR(msg) do { \
    std::ostringstream _uls; _uls << msg; \
    std::cerr << "[ULBridge] ERROR: " << _uls.str() << std::endl; \
    fireEvent(ULEventType::Error, "", json{{"message", _uls.str()}}); \
} while (false)

#ifdef _MSC_VER
#define ULBAPI __declspec(dllexport)
#else
#define ULBAPI
#endif

using namespace ultralight;

// =============================================================================
// Global State
// =============================================================================

RefPtr<Renderer> renderer;

// Unified callback: (eventType, viewName, jsonData)
typedef void (*UnifiedEventCallback)(int, const char*, const char*);
static UnifiedEventCallback eventCallback = nullptr;

// =============================================================================
// Background Thread & Command Queue
// =============================================================================

// Command types for the message queue
struct CmdInit { bool gpu; std::string resourcePath; };
struct CmdShutdown {};
struct CmdViewCreate { std::string name; int w; int h; };
struct CmdViewDelete { std::string name; };
struct CmdViewLoadHtml { std::string name; std::string html; };
struct CmdViewEvalScript { std::string name; std::string script; };
struct CmdViewResize { std::string name; int w; int h; };
struct CmdViewMouseEvent { std::string name; int x; int y; int type; int button; };
struct CmdViewScrollEvent { std::string name; int x; int y; int type; };
struct CmdViewKeyEvent { std::string name; int type; int vcode; int mods; };
struct CmdViewFocus { std::string name; };
struct CmdViewUnfocus { std::string name; };
struct CmdRegisterImage { std::string id; std::vector<unsigned char> pixels; int width; int height; };

using BridgeCmd = std::variant<
    CmdInit,
    CmdShutdown,
    CmdViewCreate,
    CmdViewDelete,
    CmdViewLoadHtml,
    CmdViewEvalScript,
    CmdViewResize,
    CmdViewMouseEvent,
    CmdViewScrollEvent,
    CmdViewKeyEvent,
    CmdViewFocus,
    CmdViewUnfocus,
    CmdRegisterImage
>;

// Thread-safe command queue
static std::queue<BridgeCmd> commandQueue;
static std::mutex queueMutex;
static std::condition_variable queueCV;
static std::atomic<bool> running{false};
static std::atomic<bool> initialized{false};
static std::thread backgroundThread;

// Event queue - events are queued during command processing and fired after render
struct QueuedEvent {
    ULEventType type;
    std::string viewName;
    json data;
};
static std::queue<QueuedEvent> eventQueue;
static std::mutex eventQueueMutex;

// Target frame rate for the background loop (60 FPS)
static constexpr int TARGET_FPS = 60;
static constexpr auto FRAME_DURATION = std::chrono::microseconds(1000000 / TARGET_FPS);

// Forward declarations for command processing
static void processCommand(const BridgeCmd& cmd);
static void backgroundLoop();

// Enqueue a command to be processed on the background thread
template<typename T>
static void enqueueCommand(T&& cmd) {
    {
        std::lock_guard<std::mutex> lock(queueMutex);
        commandQueue.push(std::forward<T>(cmd));
    }
    queueCV.notify_one();
}

// =============================================================================
// Utility Functions
// =============================================================================

static std::string utf16ToUTF8(const void* data, size_t len)
{
    const uint16_t* src = static_cast<const uint16_t*>(data);
    std::string result;
    result.reserve(len * 3);
    utf8::utf16to8(src, src + len, std::back_inserter(result));
    return result;
}

std::string toUTF8(const String16& s)
{
    return utf16ToUTF8(s.udata(), s.length());
}

// Fire a unified event to C#
static void fireEvent(ULEventType type, const char* viewName, const json& data)
{
    if (eventCallback) {
        eventCallback(static_cast<int>(type), viewName ? viewName : "", data.dump().c_str());
    }
}

// Queue an event to be fired after the current render cycle completes
static void queueEvent(ULEventType type, const char* viewName, const json& data)
{
    std::lock_guard<std::mutex> lock(eventQueueMutex);
    eventQueue.push(QueuedEvent{type, viewName ? viewName : "", data});
}

// Drain and fire all queued events (called after render cycle)
static void drainEventQueue()
{
    std::queue<QueuedEvent> localQueue;
    {
        std::lock_guard<std::mutex> lock(eventQueueMutex);
        std::swap(localQueue, eventQueue);
    }
    
    while (!localQueue.empty()) {
        const auto& evt = localQueue.front();
        fireEvent(evt.type, evt.viewName.c_str(), evt.data);
        localQueue.pop();
    }
}

class BridgeListener;
class ConsoleViewListener;

// Forward declaration for boundViewData map
struct ViewData;
static std::unordered_map<JSObjectRef, ViewData*> boundViewData;

struct ViewData
{
    RefPtr<View> view;
    RefPtr<Bitmap> bitmap;
    bool domReady = false;
    std::vector<std::string> pendingJS;
    std::unique_ptr<BridgeListener> listener;
    std::unique_ptr<ConsoleViewListener> viewListener;
    std::string securityToken;
    JSObjectRef boundFunc = nullptr;  // The JS function bound to this view
    
    void dispose() {
        if (boundFunc) {
            boundViewData.erase(boundFunc);
            boundFunc = nullptr;
        }
        
        if (bitmap) {
            bitmap->UnlockPixels();
            bitmap = nullptr;
        }
        
        if (view) {
            view->set_load_listener(nullptr);
            view->set_view_listener(nullptr);
            view->set_network_listener(nullptr);
        }
    }
    
    ~ViewData() {
        dispose();
    }
};

static std::unordered_map<std::string, ViewData> views;
static std::mutex viewsLock;

// Path to Ultralight's internal resources (icudt67l.dat, cacert.pem)
static std::string ultralightResourcesPath;

// Path constants
static constexpr const char* kResourcesSubdir = "/resources/";
static constexpr const char* kDefaultBasePath = ".";

// =============================================================================
// Custom FileSystem (handles .imgsrc files for ImageSourceProvider)
// =============================================================================

class GameAssetFileSystem : public FileSystem
{
private:
    std::filesystem::path rootPath;
    
    bool isImgSrcPath(const std::filesystem::path& path) const {
        return path.extension() == ".imgsrc";
    }
    
    std::string extractId(const std::filesystem::path& path) const {
        return path.stem().string();
    }
    
    // Securely resolve and validate path stays within resources folder
    // Returns empty path if invalid/escaped
    std::filesystem::path resolveAndValidate(const std::string& requestedPath) const {
        try {
            std::filesystem::path fullPath = rootPath / requestedPath;
            
            // Normalize: resolve . and .. components
            std::filesystem::path normalized = std::filesystem::weakly_canonical(fullPath);
            std::filesystem::path normalizedRoot = std::filesystem::weakly_canonical(rootPath);
            
            // Security check: ensure resolved path is still under resources root
            // Use lexically_relative to check containment
            std::filesystem::path rel = normalized.lexically_relative(normalizedRoot);
            
            // Convert to generic string for cross-platform comparison
            std::string relStr = rel.generic_string();
            if (rel.empty() || relStr.find("..") == 0) {
                ULERR("SECURITY: Path escape attempt blocked: " 
                      << requestedPath << " -> " << normalized 
                      << " (must stay within " << normalizedRoot << ")");
                return {};
            }
            
            return normalized;
        } catch (const std::filesystem::filesystem_error& e) {
            ULERR("Path resolution error: " << e.what());
            return {};
        }
    }

public:
    GameAssetFileSystem(const std::string& root) : rootPath(std::filesystem::weakly_canonical(root)) {
    }
    
    virtual bool FileExists(const String& path) override {
        std::string pathStr = toUTF8(path.utf16());
        std::filesystem::path resolved = resolveAndValidate(pathStr);

        if (resolved.empty()) {
            return false;
        }
        
        if (isImgSrcPath(resolved)) {
            ULDEBUG("FileExists(.imgsrc): " << resolved << " -> true");
            return true;
        }
        
        return std::filesystem::exists(resolved);
    }
    
    virtual RefPtr<Buffer> OpenFile(const String& path) override {
        std::string pathStr = toUTF8(path.utf16());
        std::filesystem::path resolved = resolveAndValidate(pathStr);

        if (resolved.empty()) {
            return nullptr;
        }
        
        if (isImgSrcPath(resolved)) {
            std::string id = extractId(resolved);
            ULDEBUG("OpenFile(.imgsrc): " << resolved << " -> id=" << id);
            std::string content = "IMGSRC-V1\n" + id;
            return Buffer::CreateFromCopy(content.data(), content.size());
        }
        
        std::ifstream file(resolved, std::ios::binary | std::ios::ate);
        if (!file.is_open()) {
            ULERR("Failed to open file: " << resolved);
            return nullptr;
        }
        
        std::streamsize size = file.tellg();
        file.seekg(0, std::ios::beg);
        
        std::vector<char> buffer(size);
        if (!file.read(buffer.data(), size)) {
            ULERR("Failed to read file: " << resolved);
            return nullptr;
        }
        
        return Buffer::CreateFromCopy(buffer.data(), buffer.size());
    }
    
    virtual String GetFileMimeType(const String& path) override {
        std::string pathStr = toUTF8(path.utf16());
        
        if (isImgSrcPath(pathStr)) {
            return String("text/plain");
        }
        
        size_t dot = pathStr.rfind('.');
        if (dot != std::string::npos) {
            std::string ext = pathStr.substr(dot);
            if (ext == ".html" || ext == ".htm") return String("text/html");
            if (ext == ".js") return String("application/javascript");
            if (ext == ".css") return String("text/css");
            if (ext == ".png") return String("image/png");
            if (ext == ".jpg" || ext == ".jpeg") return String("image/jpeg");
            if (ext == ".gif") return String("image/gif");
            if (ext == ".svg") return String("image/svg+xml");
            if (ext == ".json") return String("application/json");
            if (ext == ".dat") return String("application/octet-stream");
        }
        
        return String("application/unknown");
    }
    
    virtual String GetFileCharset(const String& path) override {
        return String("utf-8");
    }
};

static std::unique_ptr<GameAssetFileSystem> gameAssetFS;

// =============================================================================
// Network Security (blocks all external requests)
// =============================================================================

class BlockingNetworkListener : public NetworkListener
{
public:
    virtual bool OnNetworkRequest(View* caller, NetworkRequest& request) override
    {
        std::string url = toUTF8(request.url().utf16());
        ULERR("BLOCKED network request: " << url);
        return false;
    }
};

static std::unique_ptr<BlockingNetworkListener> networkListener;

// =============================================================================
// Console Message Listener (ViewListener for JS errors and console messages)
// =============================================================================

class ConsoleViewListener : public ViewListener
{
public:
    ConsoleViewListener(std::string name, View* view)
        : viewName(name)
    {
        view->set_view_listener(this);
    }

    virtual void OnChangeCursor(ultralight::View* caller,
                                ultralight::Cursor cursor) override
    {
        queueEvent(ULEventType::Cursor, viewName.c_str(), json{
            {"cursorType", static_cast<int>(cursor)}
        });
    }

    virtual void OnAddConsoleMessage(ultralight::View* caller,
                                     const ultralight::ConsoleMessage& message) override
    {
        std::string msg = toUTF8(message.message().utf16());
        std::string srcId = toUTF8(message.source_id().utf16());
        int level = static_cast<int>(message.level());
        int line = static_cast<int>(message.line_number());
        int column = static_cast<int>(message.column_number());
        
        // Queue unified console event (fired after render cycle)
        queueEvent(ULEventType::Console, viewName.c_str(), json{
            {"level", level},
            {"message", msg},
            {"sourceId", srcId},
            {"line", line},
            {"column", column}
        });
    }

private:
    std::string viewName;
};

// =============================================================================
// JavaScript Bridge
// =============================================================================

// Overload for JavaScriptCore JSStringRef
std::string toUTF8(JSStringRef& str)
{
    return utf16ToUTF8(JSStringGetCharactersPtr(str), JSStringGetLength(str));
}

JSValueRef native_call(JSContextRef ctx, JSObjectRef function,
                       JSObjectRef thisObject, size_t argumentCount,
                       const JSValueRef arguments[], JSValueRef* exception)
{
    // Bounds check: require 3 arguments (token, command name, args)
    if (argumentCount < 3) {
        return JSValueMakeNull(ctx);
    }

    auto it = boundViewData.find(function);
    if (it == boundViewData.end() || it->second == nullptr) {
        return JSValueMakeNull(ctx);
    }
    ViewData* vd = it->second;

    JSStringRef jToken = JSValueToStringCopy(ctx, arguments[0], exception);
    std::string token = toUTF8(jToken);
    JSStringRelease(jToken);

    // Validate token against this view's token
    if (vd->securityToken != token) {
        return JSValueMakeNull(ctx);
    }

    JSStringRef jName = JSValueToStringCopy(ctx, arguments[1], exception);
    JSStringRef jArg = JSValueToStringCopy(ctx, arguments[2], exception);
    std::string cmdName = toUTF8(jName);
    std::string args = toUTF8(jArg);

    JSStringRelease(jName);
    JSStringRelease(jArg);

    queueEvent(ULEventType::Command, "", json{
        {"command", cmdName},
        {"args", args}
    });

    return JSValueMakeNull(ctx);
}

class BridgeListener : public LoadListener
{
public:
    BridgeListener(std::string name, View* view)
        : name(name)
    {
        view->set_load_listener(this);
    }

    virtual void OnBeginLoading(View* caller, uint64_t frame_id,
                                 bool is_main_frame, const String& url) override
    {
        if (!is_main_frame) return;
        
        std::string urlStr = toUTF8(url.utf16());
        fireLoadEvent(0, frame_id, urlStr, "", "", 0);
    }

    virtual void OnFinishLoading(View* caller, uint64_t frame_id,
                                  bool is_main_frame, const String& url) override
    {
        if (!is_main_frame) return;
        
        std::string urlStr = toUTF8(url.utf16());
        fireLoadEvent(1, frame_id, urlStr, "", "", 0);
    }

    virtual void OnFailLoading(View* caller, uint64_t frame_id,
                                bool is_main_frame, const String& url,
                                const String& description, const String& error_domain,
                                int error_code) override
    {
        if (!is_main_frame) return;
        
        std::string urlStr = toUTF8(url.utf16());
        std::string descStr = toUTF8(description.utf16());
        std::string domainStr = toUTF8(error_domain.utf16());
        
        ULDEBUG("OnFailLoading: url=" << urlStr << " desc=" << descStr << " domain=" << domainStr << " code=" << error_code);
        
        fireLoadEvent(2, frame_id, urlStr, descStr, domainStr, error_code);
    }

    virtual void OnDOMReady(View* view, uint64_t frame_id,
                            bool is_main_frame, const String& url) override;

    virtual void OnWindowObjectReady(View* caller, uint64_t frame_id,
                                      bool is_main_frame, const String& url) override
    {
        if (!is_main_frame) return;
        
        std::string urlStr = toUTF8(url.utf16());
        fireLoadEvent(4, frame_id, urlStr, "", "", 0);
    }

    std::string name;

    void fireLoadEvent(int loadEventType, uint64_t frameId,
                       const std::string& url, const std::string& errorDesc,
                       const std::string& errorDomain, int errorCode)
    {
        json data = {
            {"loadEventType", loadEventType},
            {"frameId", frameId},
            {"url", url}
        };
        if (!errorDesc.empty() || errorCode != 0) {
            data["errorDescription"] = errorDesc;
            data["errorDomain"] = errorDomain;
            data["errorCode"] = errorCode;
        }
        queueEvent(ULEventType::Load, name.c_str(), data);
    }
};

void BridgeListener::OnDOMReady(View* view, uint64_t frame_id,
                                bool is_main_frame, const String& url)
{
    if (!is_main_frame) return;
    
    std::lock_guard lg(viewsLock);
    auto& v = views[name];
    v.domReady = true;

    // Install native call handler as non-enumerable, non-configurable property
    auto wctx = view->LockJSContext();
    auto ctx = wctx->ctx();
    
    // Create the native function and bind to ViewData
    JSStringRef jsName = JSStringCreateWithUTF8CString("__ulb_nc__");
    JSObjectRef func = JSObjectMakeFunctionWithCallback(ctx, jsName, native_call);
    
    // Store bidirectional binding
    v.boundFunc = func;
    boundViewData[func] = &v;
    
    JSObjectRef globalObj = JSContextGetGlobalObject(ctx);
    
    // Set as non-enumerable, non-configurable to hide from Object.keys() etc
    JSPropertyAttributes attrs = kJSPropertyAttributeReadOnly | 
                                  kJSPropertyAttributeDontEnum | 
                                  kJSPropertyAttributeDontDelete;
    JSObjectSetProperty(ctx, globalObj, jsName, func, attrs, nullptr);
    JSStringRelease(jsName);

    // Now execute any pending scripts (they can use the native call with token)
    for (const auto& js : v.pendingJS) {
        view->EvaluateScript(String(js.c_str()));
    }
    v.pendingJS.clear();

    // Fire DOMReady load event (after scripts are injected so JS API is available)
    std::string urlStr = toUTF8(url.utf16());
    fireLoadEvent(3, frame_id, urlStr, "", "", 0);
}

// =============================================================================
// Initialization (Internal - called on background thread)
// =============================================================================

static void doInit(bool gpu, const std::string& resourcePath)
{
    ULDEBUG("ULBRIDGE INIT (background thread)");

    // Determine base path for Ultralight internal resources
    std::string basePath = !resourcePath.empty() ? resourcePath : kDefaultBasePath;
    
    std::string resourcesPath = basePath + kResourcesSubdir;
    ultralightResourcesPath = resourcesPath;
    
    ULLOG("Ultralight internal resources: " << resourcesPath);

    // Configure Ultralight
    Config config;
    config.resource_path_prefix = String(resourcesPath.c_str());

    Platform::instance().set_config(config);
    Platform::instance().set_font_loader(GetPlatformFontLoader());

    gameAssetFS = std::make_unique<GameAssetFileSystem>(resourcesPath);
    Platform::instance().set_file_system(gameAssetFS.get());
    
    Platform::instance().set_logger(GetDefaultLogger("ultralight.log"));

    // Create the renderer
    renderer = Renderer::Create();
    
    initialized = true;
}

// Register an image with Ultralight's ImageSourceProvider singleton (internal).
static bool doRegisterImage(const std::string& id, const std::vector<unsigned char>& pixels, int width, int height)
{
    if (id.empty() || pixels.empty() || width <= 0 || height <= 0) {
        ULERR("Invalid parameters for register_image");
        return false;
    }
    
    RefPtr<Bitmap> bitmap = Bitmap::Create(width, height, BitmapFormat::BGRA8_UNORM_SRGB);
    void* dst = bitmap->LockPixels();
    memcpy(dst, pixels.data(), width * height * 4);
    bitmap->UnlockPixels();
    
    RefPtr<ImageSource> imageSource = ImageSource::CreateFromBitmap(bitmap);
    ImageSourceProvider::instance().AddImageSource(String(id.c_str()), imageSource);

    return true;
}

// =============================================================================
// Internal View Operations (called on background thread only)
// =============================================================================

static std::string generateSecurityToken() {
    unsigned char bytes[16];
    randombytes_buf(bytes, sizeof(bytes));
    
    char buf[33];
    for (int i = 0; i < 16; i++) {
        snprintf(buf + i * 2, 3, "%02x", bytes[i]);
    }
    return std::string(buf);
}

static void doViewCreate(const std::string& name, int w, int h)
{
    ViewConfig viewConfig;
    viewConfig.is_accelerated = false;  // Use CPU renderer
    viewConfig.is_transparent = true;
    viewConfig.initial_device_scale = 1.0;

    RefPtr<View> view = renderer->CreateView(w, h, viewConfig, nullptr);
    std::lock_guard<std::mutex> lock(viewsLock);
    auto [it, inserted] = views.try_emplace(name);
    auto& vd = it->second;
    vd.view = view;
    vd.bitmap = nullptr;
    vd.domReady = false;
    vd.listener = std::make_unique<BridgeListener>(name, view.get());
    vd.viewListener = std::make_unique<ConsoleViewListener>(name, view.get());
    vd.securityToken = generateSecurityToken();

    if (!networkListener) {
        networkListener = std::make_unique<BlockingNetworkListener>();
    }
    view->set_network_listener(networkListener.get());
    
    // Queue ViewCreated event with the security token (fired after render cycle)
    queueEvent(ULEventType::ViewCreated, name.c_str(), json{
        {"securityToken", vd.securityToken}
    });
}

static void doViewDelete(const std::string& name)
{
    std::lock_guard<std::mutex> lock(viewsLock);
    auto it = views.find(name);
    if (it == views.end()) {
        ULERR("doViewDelete: view not found: " << name);
        return;
    }
    views.erase(it);
}

static void doViewLoadHtml(const std::string& name, const std::string& html)
{
    std::lock_guard<std::mutex> lock(viewsLock);
    auto it = views.find(name);
    if (it == views.end()) {
        ULERR("doViewLoadHtml: view not found");
        return;
    }
    it->second.domReady = false;
    it->second.view->LoadHTML(html.c_str(), "file:///asset/");
}

static void doViewEvalScript(const std::string& name, const std::string& script)
{
    std::lock_guard<std::mutex> lock(viewsLock);
    auto it = views.find(name);
    if (it == views.end()) {
        ULERR("Dropping evalscript for " << name << ": view does not exist");
        return;
    }

    auto& v = it->second;
    if (!v.domReady) {
        v.pendingJS.push_back(script);
    } else {
        v.view->EvaluateScript(String(script.c_str()));
    }
}

static void doViewResize(const std::string& name, int w, int h)
{
    std::lock_guard<std::mutex> lock(viewsLock);
    auto it = views.find(name);
    if (it == views.end()) {
        ULERR("doViewResize: view not found");
        return;
    }
    it->second.view->Resize(w, h);
}

static void doViewMouseEvent(const std::string& name, int x, int y, int type, int button)
{
    std::lock_guard<std::mutex> lock(viewsLock);
    auto it = views.find(name);
    if (it == views.end()) {
        return;  // Silently ignore mouse events for non-existent views
    }
    MouseEvent evt{(MouseEvent::Type)type, x, y, (MouseEvent::Button)button};
    it->second.view->FireMouseEvent(evt);
}

static void doViewScrollEvent(const std::string& name, int x, int y, int type)
{
    std::lock_guard<std::mutex> lock(viewsLock);
    auto it = views.find(name);
    if (it == views.end()) {
        return;  // Silently ignore scroll events for non-existent views
    }
    ScrollEvent evt{(ScrollEvent::Type)type, x, y};
    it->second.view->FireScrollEvent(evt);
    renderer->RefreshDisplay(it->second.view->display_id());
}

static void doViewKeyEvent(const std::string& name, int type, int vcode, int mods)
{
    std::lock_guard<std::mutex> lock(viewsLock);
    auto it = views.find(name);
    if (it == views.end()) {
        return;
    }

    KeyEvent ke;
    
    // type: 0 = KeyUp, 1 = KeyDown, 2 = RawKeyDown, 3 = Char (from C# ULKeyEventType enum)
    switch (type) {
        case 0: ke.type = KeyEvent::kType_KeyUp; break;
        case 1: ke.type = KeyEvent::kType_KeyDown; break;
        case 2: ke.type = KeyEvent::kType_RawKeyDown; break;
        case 3: ke.type = KeyEvent::kType_Char; break;
        default: return;
    }
    
    ke.modifiers = mods;
    ke.virtual_key_code = vcode;
    ke.native_key_code = 0;
    
    if (type == 3) {
        // Char event: vcode is actually the character code
        char txt[2] = {static_cast<char>(vcode), 0};
        ke.text = String(txt);
        ke.unmodified_text = String(txt);
    } else {
        GetKeyIdentifierFromVirtualKeyCode(ke.virtual_key_code, ke.key_identifier);
    }
    
    it->second.view->FireKeyEvent(ke);
}

static void doViewFocus(const std::string& name)
{
    std::lock_guard<std::mutex> lock(viewsLock);
    auto it = views.find(name);
    if (it == views.end()) {
        return;
    }
    it->second.view->Focus();
}

static void doViewUnfocus(const std::string& name)
{
    std::lock_guard<std::mutex> lock(viewsLock);
    auto it = views.find(name);
    if (it == views.end()) {
        return;
    }
    it->second.view->Unfocus();
}

static void doShutdown()
{
    ULDEBUG("doShutdown");
    
    // ViewData destructors will clean up boundViewData entries
    std::lock_guard<std::mutex> lock(viewsLock);
    views.clear();
    renderer = nullptr;
    initialized = false;
}

// =============================================================================
// Command Processing (background thread)
// =============================================================================

static void processCommand(const BridgeCmd& cmd)
{
    std::visit([](auto&& arg) {
        using T = std::decay_t<decltype(arg)>;
        
        if constexpr (std::is_same_v<T, CmdInit>) {
            doInit(arg.gpu, arg.resourcePath);
        }
        else if constexpr (std::is_same_v<T, CmdShutdown>) {
            doShutdown();
        }
        else if constexpr (std::is_same_v<T, CmdViewCreate>) {
            doViewCreate(arg.name, arg.w, arg.h);
        }
        else if constexpr (std::is_same_v<T, CmdViewDelete>) {
            doViewDelete(arg.name);
        }
        else if constexpr (std::is_same_v<T, CmdViewLoadHtml>) {
            doViewLoadHtml(arg.name, arg.html);
        }
        else if constexpr (std::is_same_v<T, CmdViewEvalScript>) {
            doViewEvalScript(arg.name, arg.script);
        }
        else if constexpr (std::is_same_v<T, CmdViewResize>) {
            doViewResize(arg.name, arg.w, arg.h);
        }
        else if constexpr (std::is_same_v<T, CmdViewMouseEvent>) {
            doViewMouseEvent(arg.name, arg.x, arg.y, arg.type, arg.button);
        }
        else if constexpr (std::is_same_v<T, CmdViewScrollEvent>) {
            doViewScrollEvent(arg.name, arg.x, arg.y, arg.type);
        }
        else if constexpr (std::is_same_v<T, CmdViewKeyEvent>) {
            doViewKeyEvent(arg.name, arg.type, arg.vcode, arg.mods);
        }
        else if constexpr (std::is_same_v<T, CmdViewFocus>) {
            doViewFocus(arg.name);
        }
        else if constexpr (std::is_same_v<T, CmdViewUnfocus>) {
            doViewUnfocus(arg.name);
        }
        else if constexpr (std::is_same_v<T, CmdRegisterImage>) {
            doRegisterImage(arg.id, arg.pixels, arg.width, arg.height);
        }
    }, cmd);
}

// =============================================================================
// Background Thread Loop
// =============================================================================

static bool drainCommandQueue()
{
    bool processed = false;
    std::unique_lock<std::mutex> lock(queueMutex);
    while (!commandQueue.empty()) {
        BridgeCmd cmd = std::move(commandQueue.front());
        commandQueue.pop();
        lock.unlock();
        
        processCommand(cmd);
        processed = true;
        
        lock.lock();
    }
    return processed;
}

static void backgroundLoop()
{
    ULLOG("Background thread started");

    while (running && !initialized) {
        drainCommandQueue();
        std::this_thread::sleep_for(std::chrono::milliseconds(10));
    }
    
    ULLOG("Background thread initialized, entering render loop");

    while (running) {
        auto frameStart = std::chrono::steady_clock::now();
        
        drainCommandQueue();
        
        if (renderer) {
            renderer->Update();
            renderer->RefreshDisplay(0);
            renderer->Render();
        }
        
        // Sleep to maintain target frame rate
        auto frameEnd = std::chrono::steady_clock::now();
        auto elapsed = std::chrono::duration_cast<std::chrono::microseconds>(frameEnd - frameStart);
        if (elapsed < FRAME_DURATION) {
            std::this_thread::sleep_for(FRAME_DURATION - elapsed);
        }
    }
    
    // Final cleanup on thread exit
    if (initialized) {
        doShutdown();
    }
    
    ULLOG("Background thread exited");
}

// =============================================================================
// External C API (thread-safe, can be called from any thread)
// =============================================================================

extern "C" ULBAPI void ulbridge_start(bool gpu, const char* resourcePath)
{
    if (running) {
        ULERR("ulbridge_start: already running");
        return;
    }
    
    running = true;
    
    // Start background thread
    backgroundThread = std::thread(backgroundLoop);
    
    // Queue initialization command
    std::string path = (resourcePath != nullptr) ? resourcePath : "";
    enqueueCommand(CmdInit{gpu, path});
    
    ULLOG("Background thread launched");
}

extern "C" ULBAPI void ulbridge_stop()
{
    if (!running) {
        return;
    }
    
    ULLOG("Stopping background thread...");
    
    // Signal shutdown
    running = false;
    queueCV.notify_all();
    
    // Wait for thread to finish
    if (backgroundThread.joinable()) {
        backgroundThread.join();
    }
    
    ULLOG("Background thread stopped");
}

extern "C" ULBAPI bool ulbridge_is_running()
{
    return running;
}

extern "C" ULBAPI bool ulbridge_is_initialized()
{
    return initialized;
}

// Poll and fire all queued events. MUST be called from the main thread (Unity's Update)
// to ensure C# callbacks execute on the correct thread for Unity object access.
extern "C" ULBAPI void ulbridge_poll_events()
{
    drainEventQueue();
}

// Register an image (copies data and queues for background thread)
extern "C" ULBAPI bool ulbridge_register_image(const char* id, const unsigned char* pixels, int width, int height)
{
    if (!id || !pixels || width <= 0 || height <= 0) {
        ULERR("Invalid parameters for register_image");
        return false;
    }
    
    // Copy pixel data since it may be freed after this call returns
    size_t size = width * height * 4;
    std::vector<unsigned char> pixelsCopy(pixels, pixels + size);
    
    enqueueCommand(CmdRegisterImage{std::string(id), std::move(pixelsCopy), width, height});
    return true;
}

extern "C" ULBAPI void ulbridge_view_create(const char* name, int w, int h)
{
    enqueueCommand(CmdViewCreate{std::string(name), w, h});
}

extern "C" ULBAPI void ulbridge_view_delete(const char* name)
{
    enqueueCommand(CmdViewDelete{std::string(name)});
}

extern "C" ULBAPI void ulbridge_view_load_html(const char* name, const char* html)
{
    enqueueCommand(CmdViewLoadHtml{std::string(name), std::string(html)});
}

extern "C" ULBAPI void ulbridge_view_load_url(const char* name, const char* url)
{
    ULERR("load_url DISABLED for security. Use load_html instead.");
}

extern "C" ULBAPI void ulbridge_view_resize(const char* name, int w, int h)
{
    enqueueCommand(CmdViewResize{std::string(name), w, h});
}

extern "C" ULBAPI void ulbridge_view_eval_script(const char* name, const char* script)
{
    enqueueCommand(CmdViewEvalScript{std::string(name), std::string(script)});
}

extern "C" ULBAPI void ulbridge_view_mouse_event(const char* name, int x, int y, int type, int button)
{
    enqueueCommand(CmdViewMouseEvent{std::string(name), x, y, type, button});
}

extern "C" ULBAPI void ulbridge_view_scroll_event(const char* name, int x, int y, int type)
{
    enqueueCommand(CmdViewScrollEvent{std::string(name), x, y, type});
}

extern "C" ULBAPI void ulbridge_view_key_event(const char* name, int type, int vcode, int mods)
{
    enqueueCommand(CmdViewKeyEvent{std::string(name), type, vcode, mods});
}

extern "C" ULBAPI void ulbridge_view_focus(const char* name)
{
    enqueueCommand(CmdViewFocus{std::string(name)});
}

extern "C" ULBAPI void ulbridge_view_unfocus(const char* name)
{
    enqueueCommand(CmdViewUnfocus{std::string(name)});
}

// =============================================================================
// Synchronous Read Operations (require locking, called from main thread)
// =============================================================================

// Thread-local buffer for returning security token to C#
static thread_local std::string tokenBuffer;

extern "C" ULBAPI const char* ulbridge_view_get_token(const char* name)
{
    std::lock_guard<std::mutex> lock(viewsLock);
    auto it = views.find(name);
    if (it == views.end()) {
        return "";
    }
    tokenBuffer = it->second.securityToken;
    return tokenBuffer.c_str();
}

extern "C" ULBAPI bool ulbridge_view_is_dirty(const char* name)
{
    std::lock_guard<std::mutex> lock(viewsLock);
    auto it = views.find(name);
    if (it == views.end()) {
        return false;
    }

    BitmapSurface* surface = (BitmapSurface*)(it->second.view->surface());
    return !surface->dirty_bounds().IsEmpty();
}

extern "C" ULBAPI void* ulbridge_view_get_pixels(const char* name, int* w, int* h, int* stride)
{
    std::lock_guard<std::mutex> lock(viewsLock);
    auto it = views.find(name);
    if (it == views.end()) {
        return nullptr;
    }

    auto& vd = it->second;
    Surface* surface = vd.view->surface();
    BitmapSurface* bitmap_surface = (BitmapSurface*)surface;
    RefPtr<Bitmap> bitmap = bitmap_surface->bitmap();

    vd.bitmap = bitmap;
    *w = bitmap->width();
    *h = bitmap->height();
    *stride = bitmap->row_bytes();

    void* pixels = vd.bitmap->LockPixels();
    return pixels;
}

extern "C" ULBAPI int ulbridge_view_width(const char* name)
{
    std::lock_guard<std::mutex> lock(viewsLock);
    auto it = views.find(name);
    if (it == views.end() || !it->second.bitmap) {
        return 0;
    }
    return it->second.bitmap->width();
}

extern "C" ULBAPI int ulbridge_view_height(const char* name)
{
    std::lock_guard<std::mutex> lock(viewsLock);
    auto it = views.find(name);
    if (it == views.end() || !it->second.bitmap) {
        return 0;
    }
    return it->second.bitmap->height();
}

extern "C" ULBAPI int ulbridge_view_stride(const char* name)
{
    std::lock_guard<std::mutex> lock(viewsLock);
    auto it = views.find(name);
    if (it == views.end() || !it->second.bitmap) {
        return 0;
    }
    return it->second.bitmap->row_bytes();
}

extern "C" ULBAPI void ulbridge_view_unlock_pixels(const char* name)
{
    std::lock_guard<std::mutex> lock(viewsLock);
    auto it = views.find(name);
    if (it == views.end()) {
        return;
    }
    auto& vd = it->second;
    if (!vd.bitmap) {
        return;
    }
    BitmapSurface* surface = (BitmapSurface*)(vd.view->surface());
    vd.bitmap->UnlockPixels();
    vd.bitmap = nullptr;
    surface->ClearDirtyBounds();
}

extern "C" ULBAPI bool ulbridge_view_has_focus(const char* name)
{
    std::lock_guard<std::mutex> lock(viewsLock);
    auto it = views.find(name);
    if (it == views.end()) {
        return false;
    }
    return it->second.view->HasFocus();
}

// =============================================================================
// Event Callback Registration
// =============================================================================

extern "C" ULBAPI void ulbridge_set_event_callback(UnifiedEventCallback cb)
{
    ULDEBUG("SET_EVENT_CB " << cb);
    eventCallback = cb;
}
