export default function (view) {
    /*** Utils ***/
    const collectionHas = function (a, b) {
        for (let i = 0; i < a.length; i++) {
            if (a[i] === b) {
                return true;
            }
        }
        return false;
    }

    const findParentBySelector = function (elm, selector) {
        const all = document.querySelectorAll(selector);
        let cur = elm.parentNode;
        while (cur && !collectionHas(all, cur)) {
            cur = cur.parentNode;
        }
        return cur;
    }

    const Multify = {
        pluginId: "A1B2C3D4-E5F6-7890-ABCD-EF1234567890",

        configurationWrapper: document.querySelector("#configurationWrapper"),

        notificationType: {
            template: document.querySelector("#template-notification-type"),
            values: {
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
            },
            create: function (container, selected = []) {
                const notificationTypeKeys = Object.keys(Multify.notificationType.values).sort();
                for (const key of notificationTypeKeys) {
                    const template = Multify.notificationType.template.cloneNode(true).content;
                    const name = template.querySelector("[data-name=notificationTypeName]");
                    const value = template.querySelector("[data-name=notificationTypeValue]");

                    name.innerText = Multify.notificationType.values[key];
                    value.dataset.value = key;
                    value.checked = selected.includes(key);

                    container.appendChild(template);
                }
            },
            get: function (container) {
                const notificationTypes = [];
                const checkboxes = container.querySelectorAll('[data-name=notificationTypeValue]:checked');
                for (const checkbox of checkboxes) {
                    notificationTypes.push(checkbox.dataset.value);
                }
                return notificationTypes;
            }
        },
        userFilter: {
            template: document.querySelector("#template-user-filter"),
            users: [],
            populate: async function () {
                const users = await window.ApiClient.getUsers();
                Multify.userFilter.users = [];
                for (const user of users) {
                    Multify.userFilter.users.push({
                        id: user.Id,
                        name: user.Name
                    });
                }
            },
            create: function (container, selected = []) {
                for (const user of Multify.userFilter.users) {
                    const template = Multify.userFilter.template.cloneNode(true).content;
                    const name = template.querySelector("[data-name=userFilterName]");
                    const value = template.querySelector("[data-name=userFilterValue]");

                    name.innerText = user.name;
                    value.dataset.value = user.id;
                    value.checked = selected.includes(user.id);

                    container.appendChild(template);
                }
            },
            get: function (container) {
                const userFilter = [];
                const checkboxes = container.querySelectorAll('[data-name=userFilterValue]:checked');
                for (const checkbox of checkboxes) {
                    userFilter.push(checkbox.dataset.value);
                }
                return userFilter;
            }
        },
        baseConfig: {
            template: document.querySelector("#template-base"),
            addConfig: function (template, destinationType, destinationName) {
                const collapse = document.createElement("div");
                collapse.setAttribute("is", "emby-collapse");

                if (destinationName) {
                    collapse.setAttribute("title", `${destinationName} - ${destinationType}`);
                } else {
                    collapse.setAttribute("title", destinationType);
                }
                collapse.dataset.configWrapper = "1";
                const collapseContent = document.createElement("div");
                collapseContent.classList.add("collapseContent");

                collapseContent.appendChild(template);

                const btnRemove = document.createElement("button");
                btnRemove.innerText = "Remove";
                btnRemove.setAttribute("is", "emby-button");
                btnRemove.classList.add("raised", "button-warning", "block");
                btnRemove.addEventListener("click", Multify.removeConfig);

                collapseContent.appendChild(btnRemove);
                collapse.appendChild(collapseContent);

                return collapse;
            },
            setConfig: function (config, element) {
                element.querySelector("[data-name=chkEnableMovies]").checked = config.EnableMovies ?? true;
                element.querySelector("[data-name=chkEnableEpisodes]").checked = config.EnableEpisodes ?? true;
                element.querySelector("[data-name=chkEnableSeasons]").checked = config.EnableSeasons ?? true;
                element.querySelector("[data-name=chkEnableSeries]").checked = config.EnableSeries ?? true;
                element.querySelector("[data-name=chkEnableAlbums]").checked = config.EnableAlbums ?? true;
                element.querySelector("[data-name=chkEnableSongs]").checked = config.EnableSongs ?? true;
                element.querySelector("[data-name=chkEnableVideos]").checked = config.EnableVideos ?? true;
                element.querySelector("[data-name=txtWebhookName]").value = config.WebhookName || "";
                element.querySelector("[data-name=txtWebhookUri]").value = config.WebhookUri || "";
                element.querySelector("[data-name=chkSendAllProperties]").checked = config.SendAllProperties || false;
                element.querySelector("[data-name=chkTrimWhitespace]").checked = config.TrimWhitespace || false;
                element.querySelector("[data-name=chkSkipEmptyMessageBody]").checked = config.SkipEmptyMessageBody || false;
                element.querySelector("[data-name=chkEnableWebhook]").checked = config.EnableWebhook ?? true;
                element.querySelector("[data-name=txtTemplate]").value = Multify.atou(config.Template || "");

                const notificationTypeContainer = element.querySelector("[data-name=notificationTypeContainer]");
                Multify.notificationType.create(notificationTypeContainer, config.NotificationTypes);

                const userFilterContainer = element.querySelector("[data-name=userFilterContainer]");
                Multify.userFilter.create(userFilterContainer, config.UserFilter);
            },
            getConfig: function (element) {
                const config = {};

                config.EnableMovies = element.querySelector("[data-name=chkEnableMovies]").checked || false;
                config.EnableEpisodes = element.querySelector("[data-name=chkEnableEpisodes]").checked || false;
                config.EnableSeasons = element.querySelector("[data-name=chkEnableSeasons]").checked || false;
                config.EnableSeries = element.querySelector("[data-name=chkEnableSeries]").checked || false;
                config.EnableAlbums = element.querySelector("[data-name=chkEnableAlbums]").checked || false;
                config.EnableSongs = element.querySelector("[data-name=chkEnableSongs]").checked || false;
                config.EnableVideos = element.querySelector("[data-name=chkEnableVideos]").checked || false;
                config.WebhookName = element.querySelector("[data-name=txtWebhookName]").value || "";
                config.WebhookUri = element.querySelector("[data-name=txtWebhookUri]").value || "";
                config.SendAllProperties = element.querySelector("[data-name=chkSendAllProperties]").checked || false;
                config.TrimWhitespace = element.querySelector("[data-name=chkTrimWhitespace]").checked || false;
                config.SkipEmptyMessageBody = element.querySelector("[data-name=chkSkipEmptyMessageBody]").checked || false;
                config.EnableWebhook = element.querySelector("[data-name=chkEnableWebhook]").checked;
                config.Template = Multify.utoa(element.querySelector("[data-name=txtTemplate]").value || "");

                config.NotificationTypes = [];
                config.NotificationTypes = Multify.notificationType.get(element);

                config.UserFilter = [];
                config.UserFilter = Multify.userFilter.get(element);
                return config;
            }
        },
        telegram: {
            btnAdd: document.querySelector("#btnAddTelegram"),
            template: document.querySelector("#template-telegram"),
            addConfig: function (config) {
                const template = document.createElement("div");
                template.dataset.type = "telegram";
                template.appendChild(Multify.baseConfig.template.cloneNode(true).content);
                template.appendChild(Multify.telegram.template.cloneNode(true).content);

                const baseConfig = Multify.baseConfig.addConfig(template, "Telegram", config.WebhookName);
                Multify.configurationWrapper.appendChild(baseConfig);

                Multify.telegram.setConfig(config, baseConfig);
            },
            setConfig: function (config, element) {
                Multify.baseConfig.setConfig(config, element);
                element.querySelector("[data-name=txtBotToken]").value = config.BotToken || "";
                element.querySelector("[data-name=txtChatId]").value = config.ChatId || "";
                element.querySelector("[data-name=ddlParseMode]").value = config.ParseMode || "HTML";
                element.querySelector("[data-name=ddlMessageType]").value = config.MessageType ?? 0;
            },
            getConfig: function (e) {
                const config = Multify.baseConfig.getConfig(e);
                config.BotToken = e.querySelector("[data-name=txtBotToken]").value || "";
                config.ChatId = e.querySelector("[data-name=txtChatId]").value || "";
                config.ParseMode = e.querySelector("[data-name=ddlParseMode]").value || "HTML";
                config.MessageType = parseInt(e.querySelector("[data-name=ddlMessageType]").value || "0", 10);
                return config;
            }
        },
        gotify: {
            btnAdd: document.querySelector("#btnAddGotify"),
            template: document.querySelector("#template-gotify"),
            addConfig: function (config) {
                const template = document.createElement("div");
                template.dataset.type = "gotify";
                template.appendChild(Multify.baseConfig.template.cloneNode(true).content);
                template.appendChild(Multify.gotify.template.cloneNode(true).content);

                const baseConfig = Multify.baseConfig.addConfig(template, "Gotify", config.WebhookName);
                Multify.configurationWrapper.appendChild(baseConfig);

                Multify.gotify.setConfig(config, baseConfig);
            },
            setConfig: function (config, element) {
                Multify.baseConfig.setConfig(config, element);
                element.querySelector("[data-name=txtToken]").value = config.Token || "";
                element.querySelector("[data-name=txtPriority]").value = config.Priority || 0;
            },
            getConfig: function (e) {
                const config = Multify.baseConfig.getConfig(e);
                config.Token = e.querySelector("[data-name=txtToken]").value || "";
                config.Priority = e.querySelector("[data-name=txtPriority]").value || 0;
                return config;
            }
        },
        ntfy: {
            btnAdd: document.querySelector("#btnAddNtfy"),
            template: document.querySelector("#template-ntfy"),
            addConfig: function (config) {
                const template = document.createElement("div");
                template.dataset.type = "ntfy";
                template.appendChild(Multify.baseConfig.template.cloneNode(true).content);
                template.appendChild(Multify.ntfy.template.cloneNode(true).content);

                const baseConfig = Multify.baseConfig.addConfig(template, "ntfy", config.WebhookName);
                Multify.configurationWrapper.appendChild(baseConfig);

                Multify.ntfy.setConfig(config, baseConfig);
            },
            setConfig: function (config, element) {
                Multify.baseConfig.setConfig(config, element);
                element.querySelector("[data-name=txtTopic]").value = config.Topic || "";
                element.querySelector("[data-name=ddlPriority]").value = config.Priority || "3";
                element.querySelector("[data-name=chkEnableMarkdown]").checked = config.EnableMarkdown ?? true;
                element.querySelector("[data-name=txtAccessToken]").value = config.AccessToken || "";
            },
            getConfig: function (e) {
                const config = Multify.baseConfig.getConfig(e);
                config.Topic = e.querySelector("[data-name=txtTopic]").value || "";
                config.Priority = parseInt(e.querySelector("[data-name=ddlPriority]").value || "3", 10);
                config.EnableMarkdown = e.querySelector("[data-name=chkEnableMarkdown]").checked;
                config.AccessToken = e.querySelector("[data-name=txtAccessToken]").value || "";
                return config;
            }
        },
        genericWebhook: {
            btnAdd: document.querySelector("#btnAddGenericWebhook"),
            template: document.querySelector("#template-generic"),
            headerTemplate: document.querySelector("#template-generic-header"),
            fieldTemplate: document.querySelector("#template-generic-field"),
            addHeader: function (container, key = "", value = "") {
                const row = Multify.genericWebhook.headerTemplate.cloneNode(true).content;
                row.querySelector("[data-name=txtHeaderKey]").value = key;
                row.querySelector("[data-name=txtHeaderValue]").value = value;
                row.querySelector(".btnRemoveHeader").addEventListener("click", function () {
                    this.closest(".flex").remove();
                });
                container.appendChild(row);
            },
            addField: function (container, key = "", value = "") {
                const row = Multify.genericWebhook.fieldTemplate.cloneNode(true).content;
                row.querySelector("[data-name=txtFieldKey]").value = key;
                row.querySelector("[data-name=txtFieldValue]").value = value;
                row.querySelector(".btnRemoveField").addEventListener("click", function () {
                    this.closest(".flex").remove();
                });
                container.appendChild(row);
            },
            getHeaders: function (container) {
                const headers = [];
                const rows = container.querySelectorAll(".flex");
                for (const row of rows) {
                    const key = row.querySelector("[data-name=txtHeaderKey]").value;
                    const val = row.querySelector("[data-name=txtHeaderValue]").value;
                    if (key) {
                        headers.push({ Key: key, Value: val });
                    }
                }
                return headers;
            },
            getFields: function (container) {
                const fields = [];
                const rows = container.querySelectorAll(".flex");
                for (const row of rows) {
                    const key = row.querySelector("[data-name=txtFieldKey]").value;
                    const val = row.querySelector("[data-name=txtFieldValue]").value;
                    if (key) {
                        fields.push({ Key: key, Value: val });
                    }
                }
                return fields;
            },
            addConfig: function (config) {
                const template = document.createElement("div");
                template.dataset.type = "generic";
                template.appendChild(Multify.baseConfig.template.cloneNode(true).content);
                template.appendChild(Multify.genericWebhook.template.cloneNode(true).content);

                const baseConfig = Multify.baseConfig.addConfig(template, "Generic Webhook", config.WebhookName);
                Multify.configurationWrapper.appendChild(baseConfig);

                Multify.genericWebhook.setConfig(config, baseConfig);
            },
            setConfig: function (config, element) {
                Multify.baseConfig.setConfig(config, element);
                const headersContainer = element.querySelector("[data-name=headersContainer]");
                const fieldsContainer = element.querySelector("[data-name=fieldsContainer]");

                for (const header of (config.Headers || [])) {
                    Multify.genericWebhook.addHeader(headersContainer, header.Key, header.Value);
                }
                for (const field of (config.Fields || [])) {
                    Multify.genericWebhook.addField(fieldsContainer, field.Key, field.Value);
                }

                element.querySelector(".btnAddHeader").addEventListener("click", function () {
                    Multify.genericWebhook.addHeader(headersContainer);
                });
                element.querySelector(".btnAddField").addEventListener("click", function () {
                    Multify.genericWebhook.addField(fieldsContainer);
                });
            },
            getConfig: function (e) {
                const config = Multify.baseConfig.getConfig(e);
                config.Headers = Multify.genericWebhook.getHeaders(e.querySelector("[data-name=headersContainer]"));
                config.Fields = Multify.genericWebhook.getFields(e.querySelector("[data-name=fieldsContainer]"));
                return config;
            }
        },
        init: async function () {
            Multify.configurationWrapper.innerHTML = "";

            Multify.telegram.btnAdd.addEventListener("click", Multify.telegram.addConfig);
            Multify.gotify.btnAdd.addEventListener("click", Multify.gotify.addConfig);
            Multify.ntfy.btnAdd.addEventListener("click", Multify.ntfy.addConfig);
            Multify.genericWebhook.btnAdd.addEventListener("click", Multify.genericWebhook.addConfig);
            document.querySelector("#saveConfig").addEventListener("click", Multify.saveConfig);

            await Multify.userFilter.populate();
            Multify.loadConfig();
        },
        removeConfig: function (e) {
            e.preventDefault();
            findParentBySelector(e.target, '[data-config-wrapper]').remove();
        },
        saveConfig: function (e) {
            e.preventDefault();
            Dashboard.showLoadingMsg();

            const config = {};
            config.ServerUrl = document.querySelector("#txtServerUrl").value;
            config.MdblistApiKey = document.querySelector("#txtMdblistApiKey").value || "";

            config.TelegramOptions = [];
            const telegramConfigs = document.querySelectorAll("[data-type=telegram]");
            for (let i = 0; i < telegramConfigs.length; i++) {
                config.TelegramOptions.push(Multify.telegram.getConfig(telegramConfigs[i]));
            }

            config.GotifyOptions = [];
            const gotifyConfigs = document.querySelectorAll("[data-type=gotify]");
            for (let i = 0; i < gotifyConfigs.length; i++) {
                config.GotifyOptions.push(Multify.gotify.getConfig(gotifyConfigs[i]));
            }

            config.NtfyOptions = [];
            const ntfyConfigs = document.querySelectorAll("[data-type=ntfy]");
            for (let i = 0; i < ntfyConfigs.length; i++) {
                config.NtfyOptions.push(Multify.ntfy.getConfig(ntfyConfigs[i]));
            }

            config.GenericWebhookOptions = [];
            const genericConfigs = document.querySelectorAll("[data-type=generic]");
            for (let i = 0; i < genericConfigs.length; i++) {
                config.GenericWebhookOptions.push(Multify.genericWebhook.getConfig(genericConfigs[i]));
            }

            config.AdvancedSettings = {
                EnableDashboardAlerts: document.querySelector("#chkEnableDashboardAlerts").checked
            };

            window.ApiClient.updatePluginConfiguration(Multify.pluginId, config).then(Dashboard.processPluginConfigurationUpdateResult);
        },
        loadConfig: function () {
            Dashboard.showLoadingMsg();

            window.ApiClient.getPluginConfiguration(Multify.pluginId).then(function (config) {
                document.querySelector("#txtServerUrl").value = config.ServerUrl || "";
                document.querySelector("#txtMdblistApiKey").value = config.MdblistApiKey || "";

                for (let i = 0; i < (config.TelegramOptions || []).length; i++) {
                    Multify.telegram.addConfig(config.TelegramOptions[i]);
                }

                for (let i = 0; i < (config.GotifyOptions || []).length; i++) {
                    Multify.gotify.addConfig(config.GotifyOptions[i]);
                }

                for (let i = 0; i < (config.NtfyOptions || []).length; i++) {
                    Multify.ntfy.addConfig(config.NtfyOptions[i]);
                }

                for (let i = 0; i < (config.GenericWebhookOptions || []).length; i++) {
                    Multify.genericWebhook.addConfig(config.GenericWebhookOptions[i]);
                }

                document.querySelector("#chkEnableDashboardAlerts").checked = config.AdvancedSettings?.EnableDashboardAlerts ?? false;
            });

            Dashboard.hideLoadingMsg();
        },
        atou: function (b64) {
            return decodeURIComponent(escape(atob(b64)));
        },
        utoa: function (data) {
            return btoa(unescape(encodeURIComponent(data)));
        }
    }

    view.addEventListener("viewshow", async function () {
        await Multify.init();
    });
}
