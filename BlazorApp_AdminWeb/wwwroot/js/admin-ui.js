(() => {
    const root = document.documentElement;
    root.classList.add("js-ready");
    window.smartTourAdmin = window.smartTourAdmin ?? {};
    const admin = window.smartTourAdmin;
    const defaultMapCenter = {
        lat: 10.754027,
        lng: 106.705412
    };
    const locationPickers = new WeakMap();

    admin.storage = {
        get: (key) => window.localStorage.getItem(key),
        set: (key, value) => window.localStorage.setItem(key, value),
        remove: (key) => window.localStorage.removeItem(key)
    };

    admin.download = {
        text: (fileName, content, contentType) => {
            const blob = new Blob([content ?? ""], {
                type: contentType || "text/plain;charset=utf-8"
            });

            const objectUrl = URL.createObjectURL(blob);
            const anchor = document.createElement("a");
            anchor.href = objectUrl;
            anchor.download = fileName || "download.txt";
            anchor.click();
            URL.revokeObjectURL(objectUrl);
        }
    };

    const normalizeLanguageCode = (language) => {
        const normalized = (language ?? "").trim().replace(/_/g, "-").toLowerCase();
        if (!normalized) {
            return "";
        }

        const parts = normalized.split("-").filter(Boolean);
        if (!parts.length) {
            return "";
        }

        const prefix = parts[0] === "vn" ? "vi" : parts[0];
        if (parts.length === 1) {
            return prefix;
        }

        const region = prefix === "vi" && parts[1] === "vi" ? "vn" : parts[1];
        return [prefix, region, ...parts.slice(2)].join("-");
    };

    const getLanguagePrefix = (language) => normalizeLanguageCode(language).split("-")[0];

    const getVoiceKeywords = (voiceGender) => {
        if ((voiceGender ?? "").toLowerCase() === "male") {
            return ["male", "david", "mark", "james", "guy", "andrew", "christopher", "roger", "ryan", "daniel", "man", "nam", "hung", "namminh"];
        }

        if ((voiceGender ?? "").toLowerCase() === "female") {
            return ["female", "zira", "aria", "susan", "samantha", "victoria", "jenny", "sonia", "anna", "woman", "nu", "hoaimy"];
        }

        return [];
    };

    const getVoiceSignature = (voice) =>
        `${voice?.name ?? ""} ${voice?.voiceURI ?? ""}`.trim().toLowerCase();

    const loadVoicesAsync = async () => {
        if (!("speechSynthesis" in window)) {
            return [];
        }

        const existingVoices = window.speechSynthesis.getVoices();
        if (existingVoices.length) {
            return existingVoices;
        }

        return await new Promise((resolve) => {
            let settled = false;
            let timeoutId = 0;

            const complete = () => {
                if (settled) {
                    return;
                }

                settled = true;
                window.speechSynthesis.removeEventListener("voiceschanged", handleVoicesChanged);
                window.clearTimeout(timeoutId);
                resolve(window.speechSynthesis.getVoices());
            };

            const handleVoicesChanged = () => complete();

            timeoutId = window.setTimeout(complete, 750);
            window.speechSynthesis.addEventListener("voiceschanged", handleVoicesChanged, { once: true });
            window.speechSynthesis.getVoices();
        });
    };

    const matchesVoiceGender = (voiceSignature, voiceGender) => {
        const keywords = getVoiceKeywords(voiceGender);
        return !keywords.length || keywords.some((keyword) => voiceSignature.includes(keyword));
    };

    const scoreVoice = (voice, language, voiceGender, preferNativeVoice) => {
        const requestedLanguage = normalizeLanguageCode(language);
        const requestedPrefix = getLanguagePrefix(requestedLanguage);
        const voiceLanguage = normalizeLanguageCode(voice?.lang);
        const voicePrefix = getLanguagePrefix(voiceLanguage);
        const voiceSignature = getVoiceSignature(voice);
        let score = 0;

        if (requestedLanguage && voiceLanguage === requestedLanguage) {
            score += preferNativeVoice ? 80 : 45;
        } else if (requestedPrefix && voicePrefix === requestedPrefix) {
            score += preferNativeVoice ? 58 : 30;
        } else {
            score -= 100;
        }

        if (matchesVoiceGender(voiceSignature, voiceGender)) {
            score += 28;
        }

        if (voice?.default) {
            score += 10;
        }

        if (voice?.localService) {
            score += preferNativeVoice ? 16 : 6;
        }

        return score;
    };

    const pickVoice = async (language, voiceGender, preferNativeVoice) => {
        const voices = await loadVoicesAsync();
        if (!voices.length) {
            return null;
        }

        const requestedPrefix = getLanguagePrefix(language);
        const localizedVoices = voices.filter((voice) => getLanguagePrefix(voice?.lang) === requestedPrefix);
        if (!localizedVoices.length) {
            return null;
        }

        const genderKeywords = getVoiceKeywords(voiceGender);
        const matchingGenderVoices = !genderKeywords.length
            ? localizedVoices
            : localizedVoices.filter((voice) => matchesVoiceGender(getVoiceSignature(voice), voiceGender));

        if (!matchingGenderVoices.length) {
            return null;
        }

        const rankedVoices = [...matchingGenderVoices].sort((left, right) => {
            const scoreDelta = scoreVoice(right, language, voiceGender, preferNativeVoice)
                - scoreVoice(left, language, voiceGender, preferNativeVoice);

            if (scoreDelta !== 0) {
                return scoreDelta;
            }

            return getVoiceSignature(left).localeCompare(getVoiceSignature(right));
        });

        return rankedVoices[0] ?? null;
    };

    admin.tts = {
        preview: async (text, language, voiceGender, preferNativeVoice) => {
            const script = (text ?? "").trim();

            if (!script || !("speechSynthesis" in window) || typeof SpeechSynthesisUtterance === "undefined") {
                return false;
            }

            window.speechSynthesis.cancel();

            const utterance = new SpeechSynthesisUtterance(script);
            utterance.lang = (language ?? "").trim() || navigator.language || "en-US";

            const preferredVoice = await pickVoice(utterance.lang, voiceGender, !!preferNativeVoice);
            if (!preferredVoice) {
                return false;
            }

            utterance.voice = preferredVoice;
            utterance.lang = preferredVoice.lang || utterance.lang;
            window.speechSynthesis.speak(utterance);
            return true;
        },
        stop: () => {
            if ("speechSynthesis" in window) {
                window.speechSynthesis.cancel();
            }
        }
    };

    admin.audioPreview = {
        createObjectUrlFromStream: async (streamReference, contentType) => {
            if (!streamReference) {
                return "";
            }

            const arrayBuffer = await streamReference.arrayBuffer();
            const blob = new Blob([arrayBuffer], {
                type: contentType || "audio/mpeg"
            });

            return URL.createObjectURL(blob);
        },
        revokeObjectUrl: (url) => {
            if (url) {
                URL.revokeObjectURL(url);
            }
        },
        pauseElement: (element) => {
            if (!element || typeof element.pause !== "function") {
                return;
            }

            element.pause();

            if (typeof element.currentTime === "number") {
                element.currentTime = 0;
            }
        }
    };

    admin.filePreview = admin.audioPreview;

    const roundCoordinate = (value) => {
        const numericValue = Number(value);
        if (!Number.isFinite(numericValue)) {
            return 0;
        }

        return Math.round(numericValue * 1_000_000) / 1_000_000;
    };

    const getMapCoordinate = (value, fallback) => {
        const numericValue = Number(value);
        return Number.isFinite(numericValue) && numericValue !== 0 ? numericValue : fallback;
    };

    const reverseGeocode = async (latitude, longitude) => {
        const url = new URL("https://nominatim.openstreetmap.org/reverse");
        url.searchParams.set("format", "jsonv2");
        url.searchParams.set("lat", String(latitude));
        url.searchParams.set("lon", String(longitude));
        url.searchParams.set("zoom", "18");
        url.searchParams.set("addressdetails", "1");

        try {
            const response = await fetch(url.toString(), {
                headers: {
                    "Accept-Language": navigator.language || "en"
                }
            });

            if (!response.ok) {
                return "";
            }

            const payload = await response.json();
            return typeof payload.display_name === "string" ? payload.display_name : "";
        } catch {
            return "";
        }
    };

    const notifyMapChange = async (picker, latitude, longitude) => {
        if (!picker?.dotNetRef) {
            return;
        }

        const address = await reverseGeocode(latitude, longitude);

        try {
            await picker.dotNetRef.invokeMethodAsync(
                "ApplyLocationFromMap",
                roundCoordinate(latitude),
                roundCoordinate(longitude),
                address
            );
        } catch {
        }
    };

    const setMarkerPosition = (picker, latitude, longitude, focusMap) => {
        const lat = getMapCoordinate(latitude, defaultMapCenter.lat);
        const lng = getMapCoordinate(longitude, defaultMapCenter.lng);

        if (!picker.marker) {
            picker.marker = L.marker([lat, lng], { draggable: true }).addTo(picker.map);
            picker.marker.on("dragend", async () => {
                const position = picker.marker.getLatLng();
                await notifyMapChange(picker, position.lat, position.lng);
            });
        } else {
            picker.marker.setLatLng([lat, lng]);
        }

        if (focusMap) {
            picker.map.setView([lat, lng], picker.zoom);
        } else {
            picker.map.panTo([lat, lng], { animate: true });
        }
    };

    const setPickerLocationAndNotify = async (picker, latitude, longitude, focusMap) => {
        setMarkerPosition(picker, latitude, longitude, focusMap);
        await notifyMapChange(picker, latitude, longitude);
    };

    const tryUseDeviceLocation = (picker, fallbackLatitude, fallbackLongitude) => new Promise((resolve) => {
        if (!picker) {
            resolve(false);
            return;
        }

        if (!("geolocation" in navigator)) {
            void setPickerLocationAndNotify(picker, fallbackLatitude, fallbackLongitude, true)
                .finally(() => resolve(false));
            return;
        }

        navigator.geolocation.getCurrentPosition((position) => {
            void setPickerLocationAndNotify(
                picker,
                position.coords.latitude,
                position.coords.longitude,
                true)
                .finally(() => resolve(true));
        }, () => {
            void setPickerLocationAndNotify(picker, fallbackLatitude, fallbackLongitude, true)
                .finally(() => resolve(false));
        }, {
            enableHighAccuracy: true,
            timeout: 10000,
            maximumAge: 60000
        });
    });

    admin.map = {
        initializeLocationPicker: (element, dotNetRef, latitude, longitude) => {
            if (!element || typeof L === "undefined") {
                return false;
            }

            const existingPicker = locationPickers.get(element);
            if (existingPicker) {
                existingPicker.dotNetRef = dotNetRef;
                setMarkerPosition(existingPicker, latitude, longitude, true);
                existingPicker.map.invalidateSize();
                return true;
            }

            const map = L.map(element, {
                zoomControl: true
            });

            const picker = {
                dotNetRef,
                map,
                marker: null,
                zoom: 15
            };

            L.tileLayer("https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png", {
                maxZoom: 19,
                attribution: "&copy; OpenStreetMap contributors"
            }).addTo(map);

            map.on("click", async (event) => {
                setMarkerPosition(picker, event.latlng.lat, event.latlng.lng, false);
                await notifyMapChange(picker, event.latlng.lat, event.latlng.lng);
            });

            setMarkerPosition(picker, latitude, longitude, true);
            locationPickers.set(element, picker);

            window.setTimeout(() => {
                map.invalidateSize();
            }, 0);

            return true;
        },
        syncLocationPicker: (element, latitude, longitude) => {
            const picker = locationPickers.get(element);
            if (!picker) {
                return false;
            }

            setMarkerPosition(picker, latitude, longitude, false);
            picker.map.invalidateSize();
            return true;
        },
        tryUseDeviceLocation: async (element, fallbackLatitude, fallbackLongitude) => {
            const picker = locationPickers.get(element);
            if (!picker) {
                return false;
            }

            return await tryUseDeviceLocation(
                picker,
                getMapCoordinate(fallbackLatitude, defaultMapCenter.lat),
                getMapCoordinate(fallbackLongitude, defaultMapCenter.lng));
        },
        disposeLocationPicker: (element) => {
            const picker = locationPickers.get(element);
            if (!picker) {
                return;
            }

            picker.map.remove();
            locationPickers.delete(element);
        }
    };

    const seen = new WeakSet();
    let observer = null;

    const getElements = (scope, selector) => {
        const elements = [];

        if (scope instanceof Element && scope.matches(selector)) {
            elements.push(scope);
        }

        if (scope.querySelectorAll) {
            elements.push(...scope.querySelectorAll(selector));
        }

        return elements;
    };

    const applyGroupDelay = (scope) => {
        getElements(scope, "[data-stagger]").forEach((group) => {
            const step = Number(group.getAttribute("data-stagger-step") ?? 90);

            Array.from(group.querySelectorAll("[data-reveal]")).forEach((element, index) => {
                if (!element.style.transitionDelay) {
                    element.style.transitionDelay = `${index * step}ms`;
                }
            });
        });
    };

    const ensureObserver = () => {
        if (observer || !("IntersectionObserver" in window)) {
            return;
        }

        observer = new IntersectionObserver((entries) => {
            entries.forEach((entry) => {
                if (!entry.isIntersecting) {
                    return;
                }

                entry.target.classList.add("is-visible");
                observer.unobserve(entry.target);
            });
        }, {
            threshold: 0.16,
            rootMargin: "0px 0px -10% 0px"
        });
    };

    const activate = (scope = document) => {
        applyGroupDelay(scope);
        ensureObserver();

        getElements(scope, "[data-reveal]").forEach((element) => {
            if (seen.has(element)) {
                return;
            }

            seen.add(element);

            const customDelay = element.getAttribute("data-delay");
            if (customDelay && !element.style.transitionDelay) {
                element.style.transitionDelay = `${customDelay}ms`;
            }

            if (observer) {
                observer.observe(element);
            } else {
                element.classList.add("is-visible");
            }
        });
    };

    const observeMutations = () => {
        const mutationObserver = new MutationObserver((mutations) => {
            mutations.forEach((mutation) => {
                mutation.addedNodes.forEach((node) => {
                    if (node instanceof Element) {
                        activate(node);
                    }
                });
            });
        });

        mutationObserver.observe(document.body, {
            childList: true,
            subtree: true
        });
    };

    const start = () => {
        activate(document);
        observeMutations();
    };

    if (document.readyState === "loading") {
        document.addEventListener("DOMContentLoaded", start, { once: true });
    } else {
        start();
    }
})();
