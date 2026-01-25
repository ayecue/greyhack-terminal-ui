#define _CRT_SECURE_NO_WARNINGS 1

// =============================================================================
// Includes
// =============================================================================

// Ultralight
#include <Ultralight/Ultralight.h>
#include <AppCore/Platform.h>

// Standard Library
#include <filesystem>
#include <fstream>
#include <iostream>
#include <mutex>
#include <sstream>
#include <unordered_map>
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

enum class ULEventType : int {
    Command = 0,
    Console = 1,
    Cursor = 2,
    Load = 3,
    Log = 4,
    Error = 5
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
static std::vector<std::pair<std::string, std::string>> commands;
static std::mutex commandsLock;

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
        fireEvent(ULEventType::Cursor, viewName.c_str(), json{
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
        
        // Fire unified console event
        fireEvent(ULEventType::Console, viewName.c_str(), json{
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

    // Fire unified command event
    fireEvent(ULEventType::Command, "", json{
        {"command", cmdName},
        {"args", args}
    });

    // Also queue for polling if no callback registered
    if (!eventCallback) {
        std::lock_guard g(commandsLock);
        commands.emplace_back(cmdName, args);
    }
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
        fireEvent(ULEventType::Load, name.c_str(), data);
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
// Initialization
// =============================================================================

static bool initialized = false;

extern "C" ULBAPI void ulbridge_init(bool gpu, const char* resourcePath)
{
    ULDEBUG("ULBRIDGE INIT " << initialized);
    if (initialized) {
        return;
    }
    initialized = true;

    // Determine base path for Ultralight internal resources
    std::string basePath = (resourcePath != nullptr && resourcePath[0] != '\0') 
                           ? std::string(resourcePath) 
                           : kDefaultBasePath;
    
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
}

// Register an image with Ultralight's ImageSourceProvider singleton.
extern "C" ULBAPI bool ulbridge_register_image(const char* id, const unsigned char* pixels, int width, int height)
{
    if (!id || !pixels || width <= 0 || height <= 0) {
        ULERR("Invalid parameters for register_image");
        return false;
    }
    
    RefPtr<Bitmap> bitmap = Bitmap::Create(width, height, BitmapFormat::BGRA8_UNORM_SRGB);
    void* dst = bitmap->LockPixels();
    memcpy(dst, pixels, width * height * 4);
    bitmap->UnlockPixels();
    
    RefPtr<ImageSource> imageSource = ImageSource::CreateFromBitmap(bitmap);
    ImageSourceProvider::instance().AddImageSource(String(id), imageSource);

    return true;
}

// =============================================================================
// Synchronous View API
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

extern "C" ULBAPI void ulbridge_render()
{
    renderer->Render();
}

extern "C" ULBAPI void ulbridge_update()
{
    renderer->Update();
}

extern "C" ULBAPI void ulbridge_refresh_display(uint32_t display_id)
{
    renderer->RefreshDisplay(display_id);
}

extern "C" ULBAPI void ulbridge_view_create(const char* name, int w, int h)
{
    ViewConfig viewConfig;
    viewConfig.is_accelerated = false;  // Use CPU renderer
    viewConfig.is_transparent = true;
    viewConfig.initial_device_scale = 1.0;

    RefPtr<View> view = renderer->CreateView(w, h, viewConfig, nullptr);
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
}

// Thread-local buffer for returning security token to C#
static thread_local std::string tokenBuffer;

extern "C" ULBAPI const char* ulbridge_view_get_token(const char* name)
{
    auto it = views.find(name);
    if (it == views.end()) {
        return "";
    }
    tokenBuffer = it->second.securityToken;
    return tokenBuffer.c_str();
}

extern "C" ULBAPI bool ulbridge_view_is_dirty(const char* name)
{
    auto it = views.find(name);
    if (it == views.end()) {
        ULERR("View does not exist: " << name);
        return false;
    }

    BitmapSurface* surface = (BitmapSurface*)(it->second.view->surface());
    return !surface->dirty_bounds().IsEmpty();
}

extern "C" ULBAPI void* ulbridge_view_get_pixels(const char* name, int* w, int* h, int* stride)
{
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
    auto it = views.find(name);
    if (it == views.end() || !it->second.bitmap) {
        ULERR("ulbridge_view_width: invalid view or bitmap");
        return 0;
    }
    return it->second.bitmap->width();
}

extern "C" ULBAPI int ulbridge_view_height(const char* name)
{
    auto it = views.find(name);
    if (it == views.end() || !it->second.bitmap) {
        ULERR("ulbridge_view_height: invalid view or bitmap");
        return 0;
    }
    return it->second.bitmap->height();
}

extern "C" ULBAPI int ulbridge_view_stride(const char* name)
{
    auto it = views.find(name);
    if (it == views.end() || !it->second.bitmap) {
        ULERR("ulbridge_view_stride: invalid view or bitmap");
        return 0;
    }
    return it->second.bitmap->row_bytes();
}

extern "C" ULBAPI void ulbridge_view_unlock_pixels(const char* name)
{
    auto it = views.find(name);
    if (it == views.end()) {
        ULERR("ulbridge_view_unlock_pixels: view not found");
        return;
    }
    auto& vd = it->second;
    if (!vd.bitmap) {
        ULERR("ulbridge_view_unlock_pixels: no locked bitmap");
        return;
    }
    BitmapSurface* surface = (BitmapSurface*)(vd.view->surface());
    vd.bitmap->UnlockPixels();
    vd.bitmap = nullptr;
    surface->ClearDirtyBounds();
}

extern "C" ULBAPI void ulbridge_view_load_html(const char* name, const char* html)
{
    auto it = views.find(name);
    if (it == views.end()) {
        ULERR("ulbridge_view_load_html: view not found");
        return;
    }
    it->second.domReady = false;
    it->second.view->LoadHTML(html, "file:///asset/");
}

extern "C" ULBAPI void ulbridge_view_load_url(const char* name, const char* url)
{
    ULERR("load_url DISABLED for security. Use load_html instead.");
}

extern "C" ULBAPI void ulbridge_view_resize(const char* name, int w, int h)
{
    auto it = views.find(name);
    if (it == views.end()) {
        ULERR("ulbridge_view_resize: view not found");
        return;
    }
    it->second.view->Resize(w, h);
}

extern "C" ULBAPI void ulbridge_view_mouse_event(const char* name, int x, int y,
                                                  int type, int button)
{
    auto it = views.find(name);
    if (it == views.end()) {
        return;  // Silently ignore mouse events for non-existent views
    }
    MouseEvent evt{(MouseEvent::Type)type, x, y, (MouseEvent::Button)button};
    it->second.view->FireMouseEvent(evt);
}

extern "C" ULBAPI void ulbridge_view_scroll_event(const char* name, int x, int y, int type)
{
    auto it = views.find(name);
    if (it == views.end()) {
        return;  // Silently ignore scroll events for non-existent views
    }
    ScrollEvent evt{(ScrollEvent::Type)type, x, y};
    it->second.view->FireScrollEvent(evt);
    renderer->RefreshDisplay(it->second.view->display_id());
    renderer->Render();
}

extern "C" ULBAPI void ulbridge_view_eval_script(const char* name, const char* script)
{
    auto it = views.find(name);
    if (it == views.end()) {
        ULERR("Dropping evalscript for " << name << ": view does not exist");
        return;
    }

    auto& v = it->second;
    if (!v.domReady) {
        v.pendingJS.push_back(script);
    } else {
        v.view->EvaluateScript(String(script));
    }
}

static thread_local std::string g_selectionBuffer;

extern "C" ULBAPI const char* ulbridge_view_get_selection(const char* name)
{
    auto it = views.find(name);
    if (it == views.end()) {
        g_selectionBuffer.clear();
        return g_selectionBuffer.c_str();
    }

    auto& v = it->second;
    if (!v.domReady) {
        g_selectionBuffer.clear();
        return g_selectionBuffer.c_str();
    }

    String result = v.view->EvaluateScript(
        String("(function() { var s = window.getSelection(); return s ? s.toString() : ''; })()"));
    
    g_selectionBuffer = result.utf8().data();
    return g_selectionBuffer.c_str();
}

extern "C" ULBAPI void ulbridge_view_focus(const char* name)
{
    auto it = views.find(name);
    if (it == views.end()) {
        return;
    }
    it->second.view->Focus();
}

extern "C" ULBAPI void ulbridge_view_unfocus(const char* name)
{
    auto it = views.find(name);
    if (it == views.end()) {
        return;
    }
    it->second.view->Unfocus();
}

extern "C" ULBAPI bool ulbridge_view_has_focus(const char* name)
{
    auto it = views.find(name);
    if (it == views.end()) {
        return false;
    }
    return it->second.view->HasFocus();
}

extern "C" ULBAPI void ulbridge_view_delete(const char* name)
{
    auto it = views.find(name);
    if (it == views.end()) {
        ULERR("ulbridge_view_delete: view not found: " << name);
        return;
    }
    views.erase(it);
}

// =============================================================================
// Keyboard Input
// =============================================================================

extern "C" ULBAPI void ulbridge_view_key_event(const char* name, int type,
                                                int vcode, int mods)
{
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

// =============================================================================
// Lifecycle & Callbacks
// =============================================================================

extern "C" ULBAPI void ulbridge_shutdown()
{
    ULDEBUG("ULBSHUTDOWN");
    
    // ViewData destructors will clean up boundViewData entries
    views.clear();
    
    // Reset initialized flag to allow re-initialization
    initialized = false;
}

extern "C" ULBAPI void ulbridge_set_event_callback(UnifiedEventCallback cb)
{
    ULDEBUG("SET_EVENT_CB " << cb);
    eventCallback = cb;
}
