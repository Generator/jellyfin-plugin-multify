export default function (view) {
    const PLUGIN_ID = "A1B2C3D4-E5F6-7890-ABCD-EF1234567890";

    /*** Helpers ***/
    const $ = (sel, ctx) => (ctx || document).querySelector(sel);
    const $$ = (sel, ctx) => [...(ctx || document).querySelectorAll(sel)];

    const notificationTypes = {
        "ItemAdded": "Item Added",
        "ItemDeleted": "Item Deleted",
        "PlaybackStart": "Playback Start",
        "PlaybackProgress": "Playback Progress",
        "PlaybackStop": "Playback Stop",
        "SubtitleDownloadFailure": "Subtitle Download Failure",
        "AuthenticationFailure": "Authentication Failure",
        "AuthenticationSuccess": "Authentication Success",
        "SessionStart": "Session Start",
        "PendingRestart": "Pending Restart",
        "TaskCompleted": "Task Completed",
        "PluginInstalled": "Plugin Installed",
        "PluginUninstalled": "Plugin Uninstalled",
        "PluginUpdated": "Plugin Updated",
        "UserCreated": "User Created",
        "UserDeleted": "User Deleted",
        "UserLockedOut": "User Locked Out",
        "UserPasswordChanged": "User Password Changed",
        "UserUpdated": "User Updated",
        "UserDataSaved": "User Data Saved"
    };

    const atou = (b64) => decodeURIComponent(escape(atob(b64)));
    const utoa = (data) => btoa(unescape(encodeURIComponent(data)));

    /*** Template helpers ***/
    function cloneTemplate(id) {
        const tpl = document.getElementById(id);
        if (!tpl) return document.createElement("div");
        return tpl.cloneNode(true).content;
    }

    function buildNotificationTypeCheckboxes(selected = []) {
        const frag = document.createDocumentFragment();
        const keys = Object.keys(notificationTypes).sort();
        for (const key of keys) {
            const el = cloneTemplate("template-notification-type");
            const span = $("[data-name=notificationTypeName]", el);
            const cb = $("[data-name=notificationTypeValue]", el);
            if (span) span.textContent = notificationTypes[key];
            if (cb) {
                cb.dataset.value = key;
                cb.checked = selected.includes(key);
            }
            frag.appendChild(el);
        }
        return frag;
    }

    let usersCache = [];

    async function loadUsers() {
        try {
            const users = await window.ApiClient.getUsers();
            usersCache = users.map(u => ({ id: u.Id, name: u.Name }));
        } catch (e) {
            console.warn("Multify: Failed to load users", e);
        }
    }

    function buildUserFilterCheckboxes(selected = []) {
        const frag = document.createDocumentFragment();
        for (const user of usersCache) {
            const el = cloneTemplate("template-user-filter");
            const span = $("[data-name=userFilterName]", el);
            const cb = $("[data-name=userFilterValue]", el);
            if (span) span.textContent = user.name;
            if (cb) {
                cb.dataset.value = user.id;
                cb.checked = selected.includes(user.id);
            }
            frag.appendChild(el);
        }
        return frag;
    }

    function getCheckedValues(container, selector) {
        return $$(selector, container).filter(cb => cb.checked).map(cb => cb.dataset.value);
    }

    /*** Base destination config (shared by all types) ***/
    function buildBaseSection(config) {
        const frag = document.createDocumentFragment();

        // Name
        const nameDiv = document.createElement("div");
        nameDiv.className = "inputContainer";
        nameDiv.innerHTML = `<input is="emby-input" type="text" data-name="txtWebhookName" label="Webhook Name:"/><span>The webhook name (for display only)</span>`;
        $("input", nameDiv).value = config.WebhookName || "";
        frag.appendChild(nameDiv);

        // URI
        const uriDiv = document.createElement("div");
        uriDiv.className = "inputContainer";
        uriDiv.innerHTML = `<input is="emby-input" type="text" data-name="txtWebhookUri" label="Webhook Url:"/><span>The webhook destination url</span>`;
        $("input", uriDiv).value = config.WebhookUri || "";
        frag.appendChild(uriDiv);

        // Enable
        const enableDiv = document.createElement("div");
        enableDiv.className = "inputContainer";
        enableDiv.innerHTML = `<label class="checkboxContainer"><input is="emby-checkbox" type="checkbox" data-name="chkEnableWebhook"/><span>Enable</span></label>`;
        $("input", enableDiv).checked = config.EnableWebhook ?? true;
        frag.appendChild(enableDiv);

        // Notification types
        const ntDiv = document.createElement("div");
        ntDiv.innerHTML = `<label>Notification Type:</label><div data-name="notificationTypeContainer"></div>`;
        $("[data-name=notificationTypeContainer]", ntDiv).appendChild(buildNotificationTypeCheckboxes(config.NotificationTypes));
        frag.appendChild(ntDiv);

        // User filter
        const ufDiv = document.createElement("div");
        ufDiv.innerHTML = `<label>User Filter:</label><div data-name="userFilterContainer"></div>`;
        $("[data-name=userFilterContainer]", ufDiv).appendChild(buildUserFilterCheckboxes(config.UserFilter));
        frag.appendChild(ufDiv);

        // Item types
        const itDiv = document.createElement("div");
        itDiv.innerHTML = `
            <label>Item Type:</label>
            <div style="display:grid;grid-template-columns:1fr 1fr;gap:4px 16px;">
                <label class="checkboxContainer"><input is="emby-checkbox" type="checkbox" data-name="chkEnableMovies"/><span>Movies</span></label>
                <label class="checkboxContainer"><input is="emby-checkbox" type="checkbox" data-name="chkEnableEpisodes"/><span>Episodes</span></label>
                <label class="checkboxContainer"><input is="emby-checkbox" type="checkbox" data-name="chkEnableSeasons"/><span>Season</span></label>
                <label class="checkboxContainer"><input is="emby-checkbox" type="checkbox" data-name="chkEnableSeries"/><span>Series</span></label>
                <label class="checkboxContainer"><input is="emby-checkbox" type="checkbox" data-name="chkEnableAlbums"/><span>Albums</span></label>
                <label class="checkboxContainer"><input is="emby-checkbox" type="checkbox" data-name="chkEnableSongs"/><span>Songs</span></label>
                <label class="checkboxContainer"><input is="emby-checkbox" type="checkbox" data-name="chkEnableVideos"/><span>Videos</span></label>
            </div>`;
        const setChecked = (name, val) => { const el = $("[data-name=" + name + "]", itDiv); if (el) el.checked = val ?? true; };
        setChecked("chkEnableMovies", config.EnableMovies);
        setChecked("chkEnableEpisodes", config.EnableEpisodes);
        setChecked("chkEnableSeasons", config.EnableSeasons);
        setChecked("chkEnableSeries", config.EnableSeries);
        setChecked("chkEnableAlbums", config.EnableAlbums);
        setChecked("chkEnableSongs", config.EnableSongs);
        setChecked("chkEnableVideos", config.EnableVideos);
        frag.appendChild(itDiv);

        // Flags
        const flagsDiv = document.createElement("div");
        flagsDiv.style.marginTop = "8px";
        flagsDiv.innerHTML = `
            <label class="checkboxContainer"><input is="emby-checkbox" type="checkbox" data-name="chkSendAllProperties"/><span>Send All Properties (ignores template)</span></label>
            <label class="checkboxContainer"><input is="emby-checkbox" type="checkbox" data-name="chkTrimWhitespace"/><span>Trim leading and trailing whitespace from message body before sending</span></label>
            <label class="checkboxContainer"><input is="emby-checkbox" type="checkbox" data-name="chkSkipEmptyMessageBody"/><span>Do not send when message body is empty</span></label>`;
        const setFlag = (name, val) => { const el = $("[data-name=" + name + "]", flagsDiv); if (el) el.checked = val || false; };
        setFlag("chkSendAllProperties", config.SendAllProperties);
        setFlag("chkTrimWhitespace", config.TrimWhitespace);
        setFlag("chkSkipEmptyMessageBody", config.SkipEmptyMessageBody);
        frag.appendChild(flagsDiv);

        // Template
        const tplDiv = document.createElement("div");
        tplDiv.className = "inputContainer multify-template-section";
        tplDiv.style.marginTop = "12px";
        tplDiv.innerHTML = `<label>Template:</label><div><textarea data-name="txtTemplate" style="width: 100%; height: 400px"></textarea></div>`;
        $("textarea", tplDiv).value = atou(config.Template || "");
        frag.appendChild(tplDiv);

        return frag;
    }

    function readBaseSection(container) {
        return {
            WebhookName: $("[data-name=txtWebhookName]", container)?.value || "",
            WebhookUri: $("[data-name=txtWebhookUri]", container)?.value || "",
            EnableWebhook: $("[data-name=chkEnableWebhook]", container)?.checked ?? true,
            NotificationTypes: getCheckedValues(container, "[data-name=notificationTypeValue]:checked"),
            UserFilter: getCheckedValues(container, "[data-name=userFilterValue]:checked"),
            EnableMovies: $("[data-name=chkEnableMovies]", container)?.checked || false,
            EnableEpisodes: $("[data-name=chkEnableEpisodes]", container)?.checked || false,
            EnableSeasons: $("[data-name=chkEnableSeasons]", container)?.checked || false,
            EnableSeries: $("[data-name=chkEnableSeries]", container)?.checked || false,
            EnableAlbums: $("[data-name=chkEnableAlbums]", container)?.checked || false,
            EnableSongs: $("[data-name=chkEnableSongs]", container)?.checked || false,
            EnableVideos: $("[data-name=chkEnableVideos]", container)?.checked || false,
            SendAllProperties: $("[data-name=chkSendAllProperties]", container)?.checked || false,
            TrimWhitespace: $("[data-name=chkTrimWhitespace]", container)?.checked || false,
            SkipEmptyMessageBody: $("[data-name=chkSkipEmptyMessageBody]", container)?.checked || false,
            Template: utoa($("[data-name=txtTemplate]", container)?.value || "")
        };
    }

    /*** Destination card wrapper ***/
    function wrapDestinationCard(config, type, serviceConfigHtml, onRemove) {
        const card = document.createElement("div");
        card.className = "multify-destination-card collapsed";
        card.dataset.type = type;

        // Header
        const header = document.createElement("div");
        header.className = "card-header";
        
        // Collapse toggle button
        const toggleBtn = document.createElement("button");
        toggleBtn.className = "multify-collapse-toggle";
        toggleBtn.setAttribute("aria-expanded", "false");
        toggleBtn.setAttribute("aria-controls", "destination-content");
        toggleBtn.innerHTML = '<span class="material-icons">expand_more</span>';
        toggleBtn.addEventListener("click", (e) => {
            e.stopPropagation();
            card.classList.toggle("collapsed");
            const isExpanded = !card.classList.contains("collapsed");
            toggleBtn.setAttribute("aria-expanded", isExpanded);
            toggleBtn.innerHTML = isExpanded ? '<span class="material-icons">expand_less</span>' : '<span class="material-icons">expand_more</span>';
        });
        header.appendChild(toggleBtn);

        // Title
        const title = document.createElement("strong");
        title.className = "multify-destination-title";
        title.textContent = config.WebhookName || "Webhook Name";
        header.appendChild(title);

        // Action buttons container
        const actionsContainer = document.createElement("div");
        actionsContainer.className = "multify-destination-actions";

        // Test button (edit icon)
        const testBtn = document.createElement("button");
        testBtn.className = "multify-edit-icon";
        testBtn.setAttribute("aria-label", "Test notification");
        testBtn.innerHTML = '<span class="material-icons">edit</span>';
        testBtn.addEventListener("click", async (e) => {
            e.stopPropagation();
            card.classList.remove("collapsed");
            toggleBtn.setAttribute("aria-expanded", "true");
            await handleTestNotification(card, type, testBtn);
        });
        actionsContainer.appendChild(testBtn);

        // Remove button (trash icon)
        const removeBtn = document.createElement("button");
        removeBtn.className = "multify-trash-icon";
        removeBtn.setAttribute("aria-label", "Delete destination");
        removeBtn.innerHTML = '<span class="material-icons">delete</span>';
        removeBtn.addEventListener("click", (e) => {
            e.stopPropagation();
            card.remove();
        });
        actionsContainer.appendChild(removeBtn);

        header.appendChild(actionsContainer);
        card.appendChild(header);

        // Content container (collapsible)
        const contentContainer = document.createElement("div");
        contentContainer.className = "multify-destination-content";

        // Base config
        contentContainer.appendChild(buildBaseSection(config));

        // Service-specific
        const svcDiv = document.createElement("div");
        svcDiv.innerHTML = serviceConfigHtml;
        contentContainer.appendChild(svcDiv);

        card.appendChild(contentContainer);

        return card;
    }

    /*** Handle test notification ***/
    async function handleTestNotification(card, type, testBtn) {
        // Collect config from card
        const config = readDestinationConfig(card, type);
        if (!config) {
            Dashboard.alert("Please fill in the required fields before testing.");
            return;
        }

        // Validate required fields
        const validationError = validateDestinationConfig(type, config);
        if (validationError) {
            Dashboard.alert(validationError);
            return;
        }

        // Show loading state
        const originalText = testBtn.innerHTML;
        testBtn.innerHTML = "<span>Testing...</span>";
        testBtn.disabled = true;

        try {
            const response = await fetch("/Multify/TestNotification", {
                method: "POST",
                headers: {
                    "Content-Type": "application/json",
                    "X-Emby-Token": window.ApiClient.accessToken()
                },
                body: JSON.stringify({
                    destinationType: type,
                    config: config
                })
            });

            const result = await response.json();

            if (result.success) {
                Dashboard.alert("Test notification sent successfully!");
            } else {
                Dashboard.alert("Test notification failed: " + (result.errorMessage || "Unknown error"));
            }
        } catch (e) {
            console.error("Multify: Test notification error", e);
            Dashboard.alert("Test notification failed: " + e.message);
        } finally {
            testBtn.innerHTML = originalText;
            testBtn.disabled = false;
        }
    }

    /*** Read destination config from card ***/
    function readDestinationConfig(card, type) {
        const base = readBaseSection(card);
        const specific = {};

        if (type === "telegram") {
            specific.BotToken = $("[data-name=txtBotToken]", card)?.value || "";
            specific.ChatId = $("[data-name=txtChatId]", card)?.value || "";
            specific.ParseMode = $("[data-name=ddlParseMode]", card)?.value || "HTML";
            specific.MessageType = parseInt($("[data-name=ddlMessageType]", card)?.value || "0", 10);
            const topicIdVal = $("[data-name=txtTopicId]", card)?.value;
            specific.MessageThreadId = topicIdVal ? parseInt(topicIdVal, 10) : null;
        } else if (type === "gotify") {
            specific.Token = $("[data-name=txtToken]", card)?.value || "";
            specific.Priority = parseInt($("[data-name=txtPriority]", card)?.value || "0", 10);
        } else if (type === "ntfy") {
            specific.Topic = $("[data-name=txtTopic]", card)?.value || "";
            specific.Priority = parseInt($("[data-name=ddlPriority]", card)?.value || "3", 10);
            specific.EnableMarkdown = $("[data-name=chkEnableMarkdown]", card)?.checked;
            specific.AccessToken = $("[data-name=txtAccessToken]", card)?.value || "";
        } else if (type === "generic") {
            specific.Headers = [];
            $$("[data-name=txtHeaderKey]", card).forEach((k, i) => {
                const v = $$("[data-name=txtHeaderValue]", card)[i];
                if (k.value) specific.Headers.push({ Key: k.value, Value: v?.value || "" });
            });
            specific.Fields = [];
            $$("[data-name=txtFieldKey]", card).forEach((k, i) => {
                const v = $$("[data-name=txtFieldValue]", card)[i];
                if (k.value) specific.Fields.push({ Key: k.value, Value: v?.value || "" });
            });
        }

        return { ...base, ...specific };
    }

    /*** Validate destination config ***/
    function validateDestinationConfig(type, config) {
        if (type === "telegram") {
            if (!config.BotToken) return "Bot Token is required for Telegram.";
            if (!config.ChatId) return "Chat ID is required for Telegram.";
        } else if (type === "gotify") {
            if (!config.WebhookUri) return "Server URL is required for Gotify.";
            if (!config.Token) return "Application Token is required for Gotify.";
        } else if (type === "ntfy") {
            if (!config.WebhookUri) return "Server URL is required for ntfy.";
            if (!config.Topic) return "Topic is required for ntfy.";
        } else if (type === "generic") {
            if (!config.WebhookUri) return "Webhook URL is required.";
        }
        return null;
    }

    /*** Tab Definitions ***/
    const tabs = [
        {
            id: "general",
            label: "General",
            render(container) {
                const section = document.createElement("div");

                const title = document.createElement("h3");
                title.textContent = "General Settings";
                title.style.marginBottom = "16px";
                section.appendChild(title);

                const serverDiv = document.createElement("div");
                serverDiv.className = "inputContainer";
                serverDiv.innerHTML = `<input is="emby-input" type="text" id="txtServerUrl" label="Server Url:"/><span>For linking to content. Include base url.</span>`;
                section.appendChild(serverDiv);

                const mdbDiv = document.createElement("div");
                mdbDiv.className = "inputContainer";
                mdbDiv.innerHTML = `<input is="emby-input" type="text" id="txtMdblistApiKey" label="MDBList API Key (optional):"/><span>For fetching ratings. Get your free API key from <a href="https://mdblist.com" target="_blank">mdblist.com</a>.</span>`;
                section.appendChild(mdbDiv);

                container.appendChild(section);
            }
        },
        {
            id: "telegram",
            label: "Telegram",
            render(container) {
                const title = document.createElement("h3");
                title.textContent = "Telegram Destinations";
                title.style.marginBottom = "8px";
                container.appendChild(title);

                const desc = document.createElement("p");
                desc.textContent = "Configure Telegram bot notifications. You'll need a Bot Token from @BotFather and the Chat ID for your group or channel.";
                desc.style.cssText = "opacity:0.7;margin-bottom:16px;";
                container.appendChild(desc);

                const wrapper = document.createElement("div");
                wrapper.id = "telegramWrapper";
                container.appendChild(wrapper);

                const btn = document.createElement("button");
                btn.setAttribute("is", "emby-button");
                btn.className = "raised block";
                btn.innerHTML = "<span>Add Telegram Destination</span>";
                btn.addEventListener("click", () => {
                    wrapper.appendChild(addTelegramDestination({}));
                });
                container.appendChild(btn);

                // Load saved configs
                for (const opt of currentConfig.TelegramOptions || []) {
                    wrapper.appendChild(addTelegramDestination(opt));
                }
            }
        },
        {
            id: "gotify",
            label: "Gotify",
            render(container) {
                const title = document.createElement("h3");
                title.textContent = "Gotify Destinations";
                title.style.marginBottom = "8px";
                container.appendChild(title);

                const desc = document.createElement("p");
                desc.textContent = "Configure Gotify push notifications. You'll need the server URL and an application token.";
                desc.style.cssText = "opacity:0.7;margin-bottom:16px;";
                container.appendChild(desc);

                const wrapper = document.createElement("div");
                wrapper.id = "gotifyWrapper";
                container.appendChild(wrapper);

                const btn = document.createElement("button");
                btn.setAttribute("is", "emby-button");
                btn.className = "raised block";
                btn.innerHTML = "<span>Add Gotify Destination</span>";
                btn.addEventListener("click", () => {
                    wrapper.appendChild(addGotifyDestination({}));
                });
                container.appendChild(btn);

                for (const opt of currentConfig.GotifyOptions || []) {
                    wrapper.appendChild(addGotifyDestination(opt));
                }
            }
        },
        {
            id: "ntfy",
            label: "ntfy",
            render(container) {
                const title = document.createElement("h3");
                title.textContent = "ntfy Destinations";
                title.style.marginBottom = "8px";
                container.appendChild(title);

                const desc = document.createElement("p");
                desc.textContent = "Configure ntfy push notifications. Set a topic and optionally an access token for private topics.";
                desc.style.cssText = "opacity:0.7;margin-bottom:16px;";
                container.appendChild(desc);

                const wrapper = document.createElement("div");
                wrapper.id = "ntfyWrapper";
                container.appendChild(wrapper);

                const btn = document.createElement("button");
                btn.setAttribute("is", "emby-button");
                btn.className = "raised block";
                btn.innerHTML = "<span>Add ntfy Destination</span>";
                btn.addEventListener("click", () => {
                    wrapper.appendChild(addNtfyDestination({}));
                });
                container.appendChild(btn);

                for (const opt of currentConfig.NtfyOptions || []) {
                    wrapper.appendChild(addNtfyDestination(opt));
                }
            }
        },
        {
            id: "generic",
            label: "Generic Webhook",
            shortLabel: "Generic",
            render(container) {
                const title = document.createElement("h3");
                title.textContent = "Generic Webhook Destinations";
                title.style.marginBottom = "8px";
                container.appendChild(title);

                const desc = document.createElement("p");
                desc.textContent = "Send notifications to any URL via HTTP POST. Add custom headers and fields as needed.";
                desc.style.cssText = "opacity:0.7;margin-bottom:16px;";
                container.appendChild(desc);

                const wrapper = document.createElement("div");
                wrapper.id = "genericWrapper";
                container.appendChild(wrapper);

                const btn = document.createElement("button");
                btn.setAttribute("is", "emby-button");
                btn.className = "raised block";
                btn.innerHTML = "<span>Add Generic Webhook Destination</span>";
                btn.addEventListener("click", () => {
                    wrapper.appendChild(addGenericDestination({}));
                });
                container.appendChild(btn);

                for (const opt of currentConfig.GenericWebhookOptions || []) {
                    wrapper.appendChild(addGenericDestination(opt));
                }
            }
        },
        {
            id: "advanced",
            label: "Advanced",
            render(container) {
                const title = document.createElement("h3");
                title.textContent = "Advanced Settings";
                title.style.marginBottom = "16px";
                container.appendChild(title);

                // Log Level
                const logDiv = document.createElement("div");
                logDiv.className = "inputContainer";
                logDiv.innerHTML = `
                    <div class="selectContainer">
                        <select is="emby-select" id="ddlLogLevel" label="Log Level:">
                            <option value="Information">Information</option>
                            <option value="Warning">Warning</option>
                            <option value="Debug">Debug</option>
                            <option value="Trace">Trace</option>
                        </select>
                    </div>
                    <span>Controls the minimum severity of plugin log messages. Higher values = less logging.</span>`;
                container.appendChild(logDiv);

                // Dashboard alerts
                const alertsDiv = document.createElement("div");
                alertsDiv.className = "inputContainer";
                alertsDiv.style.marginTop = "16px";
                alertsDiv.innerHTML = `
                    <label class="checkboxContainer">
                        <input is="emby-checkbox" type="checkbox" id="chkEnableDashboardAlerts"/>
                        <span>Enable Dashboard Alerts</span>
                    </label>
                    <span>Log notification events to the Jellyfin admin dashboard activity feed.</span>`;
                container.appendChild(alertsDiv);

                // Show in main menu
                const mainMenuDiv = document.createElement("div");
                mainMenuDiv.className = "inputContainer";
                mainMenuDiv.style.marginTop = "16px";
                mainMenuDiv.innerHTML = `
                    <label class="checkboxContainer">
                        <input is="emby-checkbox" type="checkbox" id="chkEnableMainMenu"/>
                        <span>Show Multify in Main Menu</span>
                    </label>
                    <span>Toggle the Multify entry in the server's main navigation. Save and refresh the client (or clear cache) to apply.</span>`;
                container.appendChild(mainMenuDiv);

                // Set current values
                setTimeout(() => {
                    const logLevel = currentConfig.AdvancedSettings?.LogLevel || "Information";
                    const ddl = document.getElementById("ddlLogLevel");
                    if (ddl) ddl.value = logLevel;
                    const chk = document.getElementById("chkEnableDashboardAlerts");
                    if (chk) chk.checked = currentConfig.AdvancedSettings?.EnableDashboardAlerts ?? false;
                    const mainMenu = document.getElementById("chkEnableMainMenu");
                    if (mainMenu) mainMenu.checked = currentConfig.EnableMainMenu ?? true;
                }, 0);
            }
        }
    ];

    /*** Destination builders ***/
    function addTelegramDestination(config) {
        const card = wrapDestinationCard(config, "telegram", `
            <div class="inputContainer"><input is="emby-input" type="text" data-name="txtBotToken" label="Bot Token:"/></div>
            <div class="inputContainer"><input is="emby-input" type="text" data-name="txtChatId" label="Chat ID:"/></div>
            <div class="selectContainer">
                <select is="emby-select" data-name="ddlParseMode" label="Parse Mode:">
                    <option value="HTML">HTML</option>
                    <option value="Markdown">Markdown</option>
                    <option value="MarkdownV2">MarkdownV2</option>
                </select>
            </div>
            <div class="selectContainer">
                <select is="emby-select" data-name="ddlMessageType" label="Message Type:">
                    <option value="0">Text (sendMessage)</option>
                    <option value="1">Photo (sendPhoto)</option>
                    <option value="2">Rich Message (sendRichMessage)</option>
                </select>
            </div>
            <div class="inputContainer"><input is="emby-input" type="number" data-name="txtTopicId" label="Forum Topic ID (optional):"/><span>For Telegram Forum Topics. Leave empty to send to the general topic.</span></div>`, null);

        setTimeout(() => {
            const setVal = (n, v) => { const el = $("[data-name=" + n + "]", card); if (el) el.value = v != null ? String(v) : ""; };
            setVal("txtBotToken", config.BotToken);
            setVal("txtChatId", config.ChatId);
            setVal("ddlParseMode", config.ParseMode || "HTML");
            setVal("ddlMessageType", config.MessageType ?? 0);
            setVal("txtTopicId", config.MessageThreadId);
        }, 0);

        return card;
    }

    function addGotifyDestination(config) {
        const card = wrapDestinationCard(config, "gotify", `
            <div class="inputContainer"><input is="emby-input" type="text" data-name="txtToken" label="Token:"/></div>
            <div class="inputContainer"><input is="emby-input" type="number" data-name="txtPriority" label="Priority:"/></div>`, null);

        setTimeout(() => {
            const el = $("[data-name=txtToken]", card); if (el) el.value = config.Token || "";
            const pr = $("[data-name=txtPriority]", card); if (pr) pr.value = config.Priority || 0;
        }, 0);

        return card;
    }

    function addNtfyDestination(config) {
        const card = wrapDestinationCard(config, "ntfy", `
            <div class="inputContainer"><input is="emby-input" type="text" data-name="txtTopic" label="Topic:"/></div>
            <div class="selectContainer">
                <select is="emby-select" data-name="ddlPriority" label="Priority:">
                    <option value="1">Min</option>
                    <option value="2">Low</option>
                    <option value="3" selected>Default</option>
                    <option value="4">High</option>
                    <option value="5">Max/Urgent</option>
                </select>
            </div>
            <div class="inputContainer"><label class="checkboxContainer"><input is="emby-checkbox" type="checkbox" data-name="chkEnableMarkdown"/><span>Enable Markdown</span></label></div>
            <div class="inputContainer"><input is="emby-input" type="text" data-name="txtAccessToken" label="Access Token (optional):"/></div>`, null);

        setTimeout(() => {
            const setVal = (n, v) => { const el = $("[data-name=" + n + "]", card); if (el) el.value = v || ""; };
            setVal("txtTopic", config.Topic);
            setVal("ddlPriority", config.Priority || "3");
            setVal("txtAccessToken", config.AccessToken);
            const md = $("[data-name=chkEnableMarkdown]", card); if (md) md.checked = config.EnableMarkdown ?? true;
        }, 0);

        return card;
    }

    function addGenericDestination(config) {
        const card = wrapDestinationCard(config, "generic", `
            <div class="inputContainer">
                <label>Custom Headers:</label>
                <div data-name="headersContainer"></div>
                <button is="emby-button" type="button" class="raised btnAddHeader"><span>Add Header</span></button>
            </div>
            <div class="inputContainer">
                <label>Custom Fields (merged into notification data):</label>
                <div data-name="fieldsContainer"></div>
                <button is="emby-button" type="button" class="raised btnAddField"><span>Add Field</span></button>
            </div>`, null);

        setTimeout(() => {
            const headersContainer = $("[data-name=headersContainer]", card);
            const fieldsContainer = $("[data-name=fieldsContainer]", card);

            function addHeaderRow(key = "", value = "") {
                const row = cloneTemplate("template-generic-header");
                const k = $("[data-name=txtHeaderKey]", row);
                const v = $("[data-name=txtHeaderValue]", row);
                if (k) k.value = key;
                if (v) v.value = value;
                const rm = $(".btnRemoveHeader", row);
                if (rm) rm.addEventListener("click", () => row.remove());
                headersContainer.appendChild(row);
            }

            function addFieldRow(key = "", value = "") {
                const row = cloneTemplate("template-generic-field");
                const k = $("[data-name=txtFieldKey]", row);
                const v = $("[data-name=txtFieldValue]", row);
                if (k) k.value = key;
                if (v) v.value = value;
                const rm = $(".btnRemoveField", row);
                if (rm) rm.addEventListener("click", () => row.remove());
                fieldsContainer.appendChild(row);
            }

            for (const h of config.Headers || []) addHeaderRow(h.Key, h.Value);
            for (const f of config.Fields || []) addFieldRow(f.Key, f.Value);

            $(".btnAddHeader", card)?.addEventListener("click", () => addHeaderRow());
            $(".btnAddField", card)?.addEventListener("click", () => addFieldRow());
        }, 0);

        return card;
    }

    /*** Read destination configs from DOM ***/
    function readDestinations(type) {
        const cards = $$("[data-type=" + type + "]");
        return cards.map(card => {
            const base = readBaseSection(card);
            const specific = {};

            if (type === "telegram") {
                specific.BotToken = $("[data-name=txtBotToken]", card)?.value || "";
                specific.ChatId = $("[data-name=txtChatId]", card)?.value || "";
                specific.ParseMode = $("[data-name=ddlParseMode]", card)?.value || "HTML";
                specific.MessageType = parseInt($("[data-name=ddlMessageType]", card)?.value || "0", 10);
                const topicIdVal = $("[data-name=txtTopicId]", card)?.value;
                specific.MessageThreadId = topicIdVal ? parseInt(topicIdVal, 10) : null;
            } else if (type === "gotify") {
                specific.Token = $("[data-name=txtToken]", card)?.value || "";
                specific.Priority = $("[data-name=txtPriority]", card)?.value || 0;
            } else if (type === "ntfy") {
                specific.Topic = $("[data-name=txtTopic]", card)?.value || "";
                specific.Priority = parseInt($("[data-name=ddlPriority]", card)?.value || "3", 10);
                specific.EnableMarkdown = $("[data-name=chkEnableMarkdown]", card)?.checked;
                specific.AccessToken = $("[data-name=txtAccessToken]", card)?.value || "";
            } else if (type === "generic") {
                specific.Headers = [];
                $$("[data-name=txtHeaderKey]", card).forEach((k, i) => {
                    const v = $$("[data-name=txtHeaderValue]", card)[i];
                    if (k.value) specific.Headers.push({ Key: k.value, Value: v?.value || "" });
                });
                specific.Fields = [];
                $$("[data-name=txtFieldKey]", card).forEach((k, i) => {
                    const v = $$("[data-name=txtFieldValue]", card)[i];
                    if (k.value) specific.Fields.push({ Key: k.value, Value: v?.value || "" });
                });
            }

            return { ...base, ...specific };
        });
    }

    /*** Dirty state tracking ***/
    let configSnapshot = null;
    let isDirty = false;

    function updateDirtyState() {
        if (!configSnapshot) return;
        const currentJson = JSON.stringify(gatherConfig());
        const snapshotJson = JSON.stringify(configSnapshot);
        isDirty = currentJson !== snapshotJson;
        const indicator = document.getElementById("dirtyIndicator");
        if (indicator) {
            indicator.style.display = isDirty ? "inline" : "none";
        }
        const banner = document.getElementById("tabWarningBanner");
        if (banner) {
            banner.style.display = isDirty ? "flex" : "none";
        }
    }

    function takeSnapshot() {
        configSnapshot = JSON.parse(JSON.stringify(gatherConfig()));
        isDirty = false;
        const indicator = document.getElementById("dirtyIndicator");
        if (indicator) indicator.style.display = "none";
        const banner = document.getElementById("tabWarningBanner");
        if (banner) banner.style.display = "none";
    }

    function markDirty() {
        isDirty = true;
        const indicator = document.getElementById("dirtyIndicator");
        if (indicator) indicator.style.display = "inline";
        const banner = document.getElementById("tabWarningBanner");
        if (banner) banner.style.display = "flex";
    }

    /*** Debounced input (180ms) — avoids excessive state updates while typing ***/
    const DEBOUNCE_MS = 180;
    const debounceTimers = new Map();

    function debounceInput(key, callback, ms = DEBOUNCE_MS) {
        if (debounceTimers.has(key)) {
            clearTimeout(debounceTimers.get(key));
        }
        debounceTimers.set(key, setTimeout(() => {
            debounceTimers.delete(key);
            callback();
        }, ms));
    }

    /*** Reactive visibility — show/hide elements based on config state ***/
    const visibilityRules = [];

    /**
     * Register a visibility rule.
     * @param {string} elementSelector - CSS selector for the element to show/hide
     * @param {function} condition - Returns true if element should be visible
     */
    function addVisibilityRule(elementSelector, condition) {
        visibilityRules.push({ selector: elementSelector, condition });
    }

    function evaluateVisibility() {
        for (const rule of visibilityRules) {
            const el = document.querySelector(rule.selector);
            if (el) {
                el.style.display = rule.condition() ? "" : "none";
            }
        }
    }

    /*** Confirm dialog helper (improved with intro-skipper accessibility patterns) ***/
    function showConfirmDialog(title, body) {
        return new Promise((resolve) => {
            const uid = Date.now();
            const titleId = "multify-confirm-title-" + uid;
            const bodyId = "multify-confirm-body-" + uid;

            const dialog = document.createElement("dialog");
            dialog.className = "multify-confirm-dialog";
            dialog.setAttribute("aria-labelledby", titleId);
            dialog.setAttribute("aria-describedby", bodyId);
            dialog.innerHTML = `
                <h3 id="${titleId}" class="multify-confirm-title">${title}</h3>
                <p id="${bodyId}" class="multify-confirm-body">${body}</p>
                <div class="multify-confirm-actions">
                    <button class="multify-confirm-btn cancel" type="button">Cancel</button>
                    <button class="multify-confirm-btn confirm" type="button">Confirm</button>
                </div>`;
            
            const cancelBtn = dialog.querySelector(".cancel");
            const confirmBtn = dialog.querySelector(".confirm");
            
            function cleanup(result) {
                dialog.close();
                dialog.remove();
                resolve(result);
            }

            cancelBtn.addEventListener("click", () => cleanup(false));
            confirmBtn.addEventListener("click", () => cleanup(true));

            // Esc key triggers the cancel event on <dialog>
            dialog.addEventListener("cancel", (e) => {
                e.preventDefault();
                cleanup(false);
            });

            // Close when clicking the backdrop
            dialog.addEventListener("click", (e) => {
                if (e.target === dialog) cleanup(false);
            });

            document.body.appendChild(dialog);
            dialog.showModal();
            // Focus cancel by default so destructive action requires deliberate intent
            cancelBtn.focus();
        });
    }

    /*** Tab switching ***/
    let activeTabId = "general";
    let currentConfig = {};

    function switchTab(tabId) {
        activeTabId = tabId;
        const content = document.getElementById("multifyTabContent");
        const nav = document.getElementById("multifyTabNav");
        content.innerHTML = "";

        // Update button states
        $$(".multify-tab-button", nav).forEach(btn => {
            btn.classList.toggle("tab-active", btn.dataset.tabId === tabId);
        });

        const tab = tabs.find(t => t.id === tabId);
        if (tab) tab.render(content);

        // Repopulate General tab fields after render
        if (tabId === "general") {
            populateGeneral(currentConfig);
        }

        // Evaluate visibility rules after tab renders
        evaluateVisibility();
    }

    /*** Save / Load ***/
    function gatherConfig() {
        // For the active tab, read from DOM (cards/fields are rendered).
        // For inactive tabs, fall back to currentConfig (kept in sync by snapshotCurrentTab).
        return {
            ServerUrl: document.getElementById("txtServerUrl")?.value || currentConfig.ServerUrl || "",
            MdblistApiKey: document.getElementById("txtMdblistApiKey")?.value || currentConfig.MdblistApiKey || "",
            TelegramOptions: activeTabId === "telegram"
                ? readDestinations("telegram")
                : (currentConfig.TelegramOptions || []),
            GotifyOptions: activeTabId === "gotify"
                ? readDestinations("gotify")
                : (currentConfig.GotifyOptions || []),
            NtfyOptions: activeTabId === "ntfy"
                ? readDestinations("ntfy")
                : (currentConfig.NtfyOptions || []),
            GenericWebhookOptions: activeTabId === "generic"
                ? readDestinations("generic")
                : (currentConfig.GenericWebhookOptions || []),
            AdvancedSettings: activeTabId === "advanced"
                ? {
                    LogLevel: document.getElementById("ddlLogLevel")?.value || "Information",
                    EnableDashboardAlerts: document.getElementById("chkEnableDashboardAlerts")?.checked ?? false
                }
                : (currentConfig.AdvancedSettings || { LogLevel: "Information", EnableDashboardAlerts: false }),
            EnableMainMenu: activeTabId === "advanced"
                ? (document.getElementById("chkEnableMainMenu")?.checked ?? true)
                : (currentConfig.EnableMainMenu ?? true)
        };
    }

    function populateGeneral(config) {
        const serverUrl = document.getElementById("txtServerUrl");
        const mdbKey = document.getElementById("txtMdblistApiKey");
        if (serverUrl) serverUrl.value = config.ServerUrl || "";
        if (mdbKey) mdbKey.value = config.MdblistApiKey || "";
    }

    /*** Init ***/
    async function init() {
        await loadUsers();

        // Detect mobile viewport
        const isMobile = window.matchMedia("(max-width: 768px)").matches;

        // Build tab nav
        const nav = document.getElementById("multifyTabNav");
        nav.innerHTML = "";
        for (const tab of tabs) {
            const btn = document.createElement("button");
            btn.className = "multify-tab-button";
            btn.dataset.tabId = tab.id;
            // Use short label on mobile if available
            btn.textContent = (isMobile && tab.shortLabel) ? tab.shortLabel : tab.label;
            btn.addEventListener("click", async () => {
                if (activeTabId === tab.id) return; // already active
                if (isDirty) {
                    const confirmed = await showConfirmDialog(
                        "Unsaved Changes",
                        "You have unsaved changes. Do you want to switch tabs without saving?"
                    );
                    if (!confirmed) return;
                }
                snapshotCurrentTab();
                switchTab(tab.id);
            });
            nav.appendChild(btn);
        }

        // Mark dirty on input changes (debounced for text/number inputs)
        document.addEventListener("input", (e) => {
            if (e.target.matches("input[type='text'], input[type='number'], textarea")) {
                debounceInput(e.target.id || e.target.name, () => {
                    updateDirtyState();
                    evaluateVisibility();
                });
            } else if (e.target.matches("select")) {
                updateDirtyState();
                evaluateVisibility();
            }
        });
        document.addEventListener("change", (e) => {
            if (e.target.matches("input, select, textarea")) {
                updateDirtyState();
                evaluateVisibility();
            }
        });

        // Load config
        Dashboard.showLoadingMsg();
        try {
            currentConfig = await window.ApiClient.getPluginConfiguration(PLUGIN_ID);
        } catch (e) {
            console.error("Multify: Failed to load config", e);
            currentConfig = {};
        }

        // Show general tab first
        switchTab("general");
        // Populate general fields after tab renders, THEN take initial snapshot
        // so the snapshot captures the correct initial state (not empty fields)
        setTimeout(() => {
            populateGeneral(currentConfig);
            // Take initial snapshot for dirty tracking
            takeSnapshot();
        }, 10);

        // Register reactive visibility rules
        // Hide Template when "Send All Properties" is checked (it's ignored in that mode)
        addVisibilityRule(
            ".multify-template-section",
            () => {
                const chk = document.querySelector("[data-name=chkSendAllProperties]");
                return chk ? !chk.checked : true;
            }
        );

        Dashboard.hideLoadingMsg();
    }

    /*** Snapshot current tab's visible fields into currentConfig ***/
    function snapshotCurrentTab() {
        if (activeTabId === "general") {
            currentConfig.ServerUrl = document.getElementById("txtServerUrl")?.value || "";
            currentConfig.MdblistApiKey = document.getElementById("txtMdblistApiKey")?.value || "";
        } else if (activeTabId === "advanced") {
            currentConfig.AdvancedSettings = {
                LogLevel: document.getElementById("ddlLogLevel")?.value || "Information",
                EnableDashboardAlerts: document.getElementById("chkEnableDashboardAlerts")?.checked ?? false
            };
            currentConfig.EnableMainMenu = document.getElementById("chkEnableMainMenu")?.checked ?? true;
        } else if (activeTabId === "telegram") {
            currentConfig.TelegramOptions = readDestinations("telegram");
        } else if (activeTabId === "gotify") {
            currentConfig.GotifyOptions = readDestinations("gotify");
        } else if (activeTabId === "ntfy") {
            currentConfig.NtfyOptions = readDestinations("ntfy");
        } else if (activeTabId === "generic") {
            currentConfig.GenericWebhookOptions = readDestinations("generic");
        }
    }

    /*** Save button ***/
    view.addEventListener("viewshow", async function () {
        await init();
    });

    // Warn before leaving with unsaved changes
    window.addEventListener("beforeunload", (e) => {
        if (isDirty) {
            e.preventDefault();
            e.returnValue = "";
        }
    });

    document.getElementById("saveConfig")?.addEventListener("click", async function (e) {
        e.preventDefault();
        
        const saveBtn = document.getElementById("saveConfig");
        const saveStatus = document.getElementById("saveStatus");
        
        // Disable save button and show saving state
        saveBtn.disabled = true;
        saveBtn.querySelector("span").textContent = "Saving...";
        
        // Show saving status
        if (saveStatus) {
            saveStatus.textContent = "Saving...";
            saveStatus.dataset.state = "info";
            saveStatus.style.display = "inline";
        }

        // Snapshot the currently active tab's visible fields into currentConfig
        snapshotCurrentTab();

        // NOTE: We do NOT re-render inactive destination tabs here.
        // Destination card values are set asynchronously (setTimeout 0) in
        // addTelegramDestination/addGotifyDestination/etc., so readDestinations()
        // would read empty values synchronously, wiping out the saved config.
        // currentConfig already holds the correct data for all inactive tabs
        // from either the initial load or previous snapshotCurrentTab() calls.

        // Build final config from currentConfig (which has all snapshotted data)
        const configToSave = {
            ServerUrl: currentConfig.ServerUrl || "",
            MdblistApiKey: currentConfig.MdblistApiKey || "",
            TelegramOptions: currentConfig.TelegramOptions || [],
            GotifyOptions: currentConfig.GotifyOptions || [],
            NtfyOptions: currentConfig.NtfyOptions || [],
            GenericWebhookOptions: currentConfig.GenericWebhookOptions || [],
            AdvancedSettings: currentConfig.AdvancedSettings || {
                LogLevel: "Information",
                EnableDashboardAlerts: false
            },
            EnableMainMenu: currentConfig.EnableMainMenu ?? true
        };

        try {
            await window.ApiClient.updatePluginConfiguration(PLUGIN_ID, configToSave);
            Dashboard.processPluginConfigurationUpdateResult({ Configuration: configToSave });
            
            // Show success status
            if (saveStatus) {
                saveStatus.textContent = "Changes saved";
                saveStatus.dataset.state = "success";
                saveStatus.style.display = "inline";
                
                // Clear status after 3 seconds
                setTimeout(() => {
                    if (saveStatus.dataset.state === "success") {
                        saveStatus.style.display = "none";
                    }
                }, 3000);
            }

            // Reset dirty state
            takeSnapshot();
        } catch (e) {
            console.error("Multify: Failed to save config", e);
            
            // Show error status
            if (saveStatus) {
                saveStatus.textContent = "Save failed";
                saveStatus.dataset.state = "error";
                saveStatus.style.display = "inline";
            }
            
            Dashboard.alert("Failed to save configuration.");
        } finally {
            // Re-enable save button
            saveBtn.disabled = false;
            saveBtn.querySelector("span").textContent = "Save";
        }
    });
}
