(() => {
    const root = document.documentElement;
    root.classList.add("js-ready");
    root.dataset.scrollDirection = "down";
    window.smartTourAdmin = window.smartTourAdmin ?? {};
    const admin = window.smartTourAdmin;
    const defaultMapCenter = {
        lat: 10.754027,
        lng: 106.705412
    };
    const routePlanningConfig = {
        baseUrl: "https://routing.openstreetmap.de/routed-foot",
        profile: "walking",
        requestTimeoutMs: 15000
    };
    const locationPickers = new WeakMap();
    const tourPlanners = new WeakMap();
    const statisticsMaps = new WeakMap();

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

    admin.shell = {
        setBodyScrollLock: (isLocked) => {
            const content = document.querySelector(".admin-shell__content");

            document.documentElement.style.overflow = isLocked ? "hidden" : "";
            document.body.style.overflow = isLocked ? "hidden" : "";
            document.body.style.touchAction = isLocked ? "none" : "";

            if (content instanceof HTMLElement) {
                content.style.overflow = isLocked ? "hidden" : "";
            }
        }
    };

    admin.mail = {
        composeInvite: async (email, subject, body) => {
            const normalizedEmail = (email ?? "").trim();
            if (!normalizedEmail) {
                return false;
            }

            const subjectValue = (subject ?? "").trim();
            const bodyValue = (body ?? "").trim();

            try {
                if (navigator.clipboard?.writeText) {
                    await navigator.clipboard.writeText(bodyValue);
                }
            } catch {
            }

            const mailtoUrl = `mailto:${encodeURIComponent(normalizedEmail)}?subject=${encodeURIComponent(subjectValue)}&body=${encodeURIComponent(bodyValue)}`;
            window.location.href = mailtoUrl;
            return true;
        }
    };

    admin.effects = {
        pinElementHeight: (targetElement, sourceElement) => {
            if (!(targetElement instanceof HTMLElement) || !(sourceElement instanceof HTMLElement)) {
                return false;
            }

            const sourceRect = sourceElement.getBoundingClientRect();
            if (sourceRect.height <= 0) {
                return false;
            }

            targetElement.style.setProperty("--statistics-slot-height", `${sourceRect.height}px`);
            return true;
        },
        clearPinnedElementHeight: (targetElement) => {
            if (!(targetElement instanceof HTMLElement)) {
                return false;
            }

            targetElement.style.removeProperty("--statistics-slot-height");
            return true;
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

    const rankVoices = (voices, language, voiceGender, preferNativeVoice) =>
        [...voices].sort((left, right) => {
            const scoreDelta = scoreVoice(right, language, voiceGender, preferNativeVoice)
                - scoreVoice(left, language, voiceGender, preferNativeVoice);

            if (scoreDelta !== 0) {
                return scoreDelta;
            }

            return getVoiceSignature(left).localeCompare(getVoiceSignature(right));
        });

    const pickVoice = async (language, voiceGender, preferNativeVoice) => {
        const voices = await loadVoicesAsync();
        if (!voices.length) {
            return null;
        }

        const requestedLanguage = normalizeLanguageCode(language);
        const requestedPrefix = getLanguagePrefix(language);
        const exactLanguageVoices = voices.filter((voice) => normalizeLanguageCode(voice?.lang) === requestedLanguage);
        const localizedVoices = voices.filter((voice) => getLanguagePrefix(voice?.lang) === requestedPrefix);
        const genderKeywords = getVoiceKeywords(voiceGender);
        const hasGenderPreference = genderKeywords.length > 0;

        const exactLanguageGenderVoices = hasGenderPreference
            ? exactLanguageVoices.filter((voice) => matchesVoiceGender(getVoiceSignature(voice), voiceGender))
            : exactLanguageVoices;
        if (exactLanguageGenderVoices.length) {
            return rankVoices(exactLanguageGenderVoices, language, voiceGender, preferNativeVoice)[0] ?? null;
        }

        const localizedGenderVoices = hasGenderPreference
            ? localizedVoices.filter((voice) => matchesVoiceGender(getVoiceSignature(voice), voiceGender))
            : localizedVoices;
        if (localizedGenderVoices.length) {
            return rankVoices(localizedGenderVoices, language, voiceGender, preferNativeVoice)[0] ?? null;
        }

        if (exactLanguageVoices.length) {
            return rankVoices(exactLanguageVoices, language, voiceGender, preferNativeVoice)[0] ?? null;
        }

        if (!localizedVoices.length) {
            return null;
        }

        return rankVoices(localizedVoices, language, voiceGender, preferNativeVoice)[0] ?? null;
    };

    const inspectVoices = async (language, voiceGender, preferNativeVoice) => {
        const voices = await loadVoicesAsync();
        const normalizedLanguage = normalizeLanguageCode(language);
        const requestedPrefix = getLanguagePrefix(normalizedLanguage);
        const selectedVoice = await pickVoice(language, voiceGender, preferNativeVoice);
        const selectedSignature = getVoiceSignature(selectedVoice);

        return voices
            .map((voice) => {
                const voiceLanguage = normalizeLanguageCode(voice?.lang);
                const voicePrefix = getLanguagePrefix(voiceLanguage);
                const voiceSignature = getVoiceSignature(voice);

                return {
                    name: voice?.name ?? "",
                    voiceUri: voice?.voiceURI ?? "",
                    lang: voice?.lang ?? "",
                    normalizedLang: voiceLanguage,
                    isDefault: !!voice?.default,
                    isLocalService: !!voice?.localService,
                    matchesRequestedLanguage: !!normalizedLanguage && voiceLanguage === normalizedLanguage,
                    matchesRequestedPrefix: !!requestedPrefix && voicePrefix === requestedPrefix,
                    matchesGender: matchesVoiceGender(voiceSignature, voiceGender),
                    score: scoreVoice(voice, language, voiceGender, preferNativeVoice),
                    isSelected: !!selectedSignature && voiceSignature === selectedSignature
                };
            })
            .sort((left, right) => {
                if (right.score !== left.score) {
                    return right.score - left.score;
                }

                return `${left.name} ${left.voiceUri}`.localeCompare(`${right.name} ${right.voiceUri}`);
            });
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
        inspectVoices: async (language, voiceGender, preferNativeVoice) =>
            await inspectVoices(language, voiceGender, !!preferNativeVoice),
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

    const escapeHtml = (value) => String(value ?? "")
        .replaceAll("&", "&amp;")
        .replaceAll("<", "&lt;")
        .replaceAll(">", "&gt;")
        .replaceAll("\"", "&quot;")
        .replaceAll("'", "&#39;");

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

    const fetchLocationJson = async (url) => {
        try {
            const response = await fetch(url.toString(), {
                headers: {
                    "Accept-Language": navigator.language || "en"
                }
            });

            if (!response.ok) {
                return null;
            }

            return await response.json();
        } catch {
            return null;
        }
    };

    const reverseGeocode = async (latitude, longitude) => {
        const url = new URL("https://nominatim.openstreetmap.org/reverse");
        url.searchParams.set("format", "jsonv2");
        url.searchParams.set("lat", String(latitude));
        url.searchParams.set("lon", String(longitude));
        url.searchParams.set("zoom", "18");
        url.searchParams.set("addressdetails", "1");

        const payload = await fetchLocationJson(url);
        if (!payload) {
            return "";
        }

        return typeof payload.display_name === "string" ? payload.display_name : "";
    };

    const searchLocationMatches = async (query) => {
        const normalizedQuery = String(query ?? "").trim();
        if (!normalizedQuery) {
            return [];
        }

        const url = new URL("https://nominatim.openstreetmap.org/search");
        url.searchParams.set("format", "jsonv2");
        url.searchParams.set("q", normalizedQuery);
        url.searchParams.set("limit", "6");
        url.searchParams.set("addressdetails", "1");
        url.searchParams.set("dedupe", "1");

        const payload = await fetchLocationJson(url);
        if (!Array.isArray(payload)) {
            return [];
        }

        return payload
            .map((item) => {
                const address = typeof item?.display_name === "string"
                    ? item.display_name.trim()
                    : "";
                const fallbackName = address.split(",")[0]?.trim() ?? "";
                const name = typeof item?.name === "string" && item.name.trim()
                    ? item.name.trim()
                    : fallbackName;
                const latitude = roundCoordinate(item?.lat);
                const longitude = roundCoordinate(item?.lon);

                return {
                    name,
                    address,
                    latitude,
                    longitude
                };
            })
            .filter((item) =>
                item.address
                && Number.isFinite(item.latitude)
                && Number.isFinite(item.longitude));
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

    const createTourMarkerIcon = (point) => {
        const isSelected = !!point?.isSelected;
        const markerClass = isSelected
            ? "tour-map-marker tour-map-marker--selected"
            : "tour-map-marker tour-map-marker--available";
        const label = isSelected
            ? String(point?.order ?? "")
            : "+";

        return L.divIcon({
            className: "tour-map-marker-shell",
            html: `<div class="${markerClass}" title="${escapeHtml(point?.name)}">${escapeHtml(label)}</div>`,
            iconSize: [36, 36],
            iconAnchor: [18, 18]
        });
    };

    const getPlannerLatLngs = (points) => points
        .filter((point) => Number.isFinite(Number(point?.latitude)) && Number.isFinite(Number(point?.longitude)))
        .map((point) => [Number(point.latitude), Number(point.longitude)]);

    const getSelectedRouteLatLngs = (points) => points
        .filter((point) => !!point?.isSelected)
        .sort((left, right) => Number(left?.order ?? 0) - Number(right?.order ?? 0))
        .filter((point) => Number.isFinite(Number(point?.latitude)) && Number.isFinite(Number(point?.longitude)))
        .map((point) => [Number(point.latitude), Number(point.longitude)]);

    const getRoutePathLatLngs = (state) => (Array.isArray(state?.routePath) ? state.routePath : [])
        .filter((point) => Number.isFinite(Number(point?.latitude)) && Number.isFinite(Number(point?.longitude)))
        .map((point) => [Number(point.latitude), Number(point.longitude)]);

    const roundDistanceKm = (value) => {
        const numericValue = Number(value);
        if (!Number.isFinite(numericValue)) {
            return 0;
        }

        return Math.round(numericValue * 100) / 100;
    };

    const normalizeTimeValue = (value) => {
        const normalized = String(value ?? "").trim();
        const match = normalized.match(/^(\d{1,2}):(\d{2})$/);
        if (!match) {
            return null;
        }

        const hours = Number(match[1]);
        const minutes = Number(match[2]);
        if (!Number.isInteger(hours)
            || !Number.isInteger(minutes)
            || hours < 0
            || hours > 23
            || minutes < 0
            || minutes > 59) {
            return null;
        }

        return `${String(hours).padStart(2, "0")}:${String(minutes).padStart(2, "0")}`;
    };

    const calculateFinishTime = (startTime, durationMinutes) => {
        const normalizedStartTime = normalizeTimeValue(startTime);
        if (!normalizedStartTime) {
            return null;
        }

        const [hours, minutes] = normalizedStartTime.split(":").map(Number);
        const totalMinutes = (hours * 60) + minutes + Math.max(0, Number(durationMinutes) || 0);
        const normalizedTotalMinutes = ((totalMinutes % 1440) + 1440) % 1440;
        const finishHours = Math.floor(normalizedTotalMinutes / 60);
        const finishMinutes = normalizedTotalMinutes % 60;
        return `${String(finishHours).padStart(2, "0")}:${String(finishMinutes).padStart(2, "0")}`;
    };

    const calculateWalkingDurationMinutes = (distanceKm, walkingSpeedKph) => {
        const normalizedDistanceKm = Math.max(0, Number(distanceKm) || 0);
        const normalizedWalkingSpeedKph = Number(walkingSpeedKph);
        if (!Number.isFinite(normalizedWalkingSpeedKph) || normalizedWalkingSpeedKph <= 0) {
            return 0;
        }

        return Math.ceil((normalizedDistanceKm / normalizedWalkingSpeedKph) * 60);
    };

    const toRadians = (value) => Number(value) * Math.PI / 180;

    const calculateDistanceKm = (fromLatitude, fromLongitude, toLatitude, toLongitude) => {
        const normalizedFromLatitude = Number(fromLatitude);
        const normalizedFromLongitude = Number(fromLongitude);
        const normalizedToLatitude = Number(toLatitude);
        const normalizedToLongitude = Number(toLongitude);
        if (!Number.isFinite(normalizedFromLatitude)
            || !Number.isFinite(normalizedFromLongitude)
            || !Number.isFinite(normalizedToLatitude)
            || !Number.isFinite(normalizedToLongitude)) {
            return 0;
        }

        const earthRadiusKm = 6371;
        const latDelta = toRadians(normalizedToLatitude - normalizedFromLatitude);
        const lonDelta = toRadians(normalizedToLongitude - normalizedFromLongitude);
        const originLatitude = toRadians(normalizedFromLatitude);
        const destinationLatitude = toRadians(normalizedToLatitude);
        const haversine =
            Math.sin(latDelta / 2) ** 2
            + Math.cos(originLatitude) * Math.cos(destinationLatitude) * Math.sin(lonDelta / 2) ** 2;
        const normalizedHaversine = Math.min(1, Math.max(0, haversine));
        const arc = 2 * Math.atan2(Math.sqrt(normalizedHaversine), Math.sqrt(1 - normalizedHaversine));
        return earthRadiusKm * arc;
    };

    const normalizeRouteStops = (stops) => (Array.isArray(stops) ? stops : [])
        .map((stop, index) => ({
            LocationId: Number(stop?.locationId ?? stop?.LocationId ?? 0),
            SequenceOrder: Number(stop?.sequenceOrder ?? stop?.SequenceOrder ?? (index + 1)),
            Latitude: Number(stop?.latitude ?? stop?.Latitude),
            Longitude: Number(stop?.longitude ?? stop?.Longitude)
        }))
        .filter((stop) =>
            stop.LocationId > 0
            && Number.isFinite(stop.SequenceOrder)
            && Number.isFinite(stop.Latitude)
            && Number.isFinite(stop.Longitude))
        .sort((left, right) => left.SequenceOrder - right.SequenceOrder || left.LocationId - right.LocationId);

    const appendRoutePoint = (path, latitude, longitude) => {
        const normalizedLatitude = Number(latitude);
        const normalizedLongitude = Number(longitude);
        if (!Number.isFinite(normalizedLatitude) || !Number.isFinite(normalizedLongitude)) {
            return;
        }

        const lastPoint = path[path.length - 1];
        if (lastPoint
            && Math.abs(lastPoint.Latitude - normalizedLatitude) < 0.000001
            && Math.abs(lastPoint.Longitude - normalizedLongitude) < 0.000001) {
            return;
        }

        path.push({
            Latitude: normalizedLatitude,
            Longitude: normalizedLongitude
        });
    };

    const buildStraightLinePreview = (stops, startTime, walkingSpeedKph, usesRoadRouting) => {
        const normalizedStops = normalizeRouteStops(stops);
        const normalizedStartTime = normalizeTimeValue(startTime);
        const normalizedWalkingSpeedKph = Number.isFinite(Number(walkingSpeedKph)) && Number(walkingSpeedKph) > 0
            ? Number(walkingSpeedKph)
            : 5;
        const segments = [];
        const path = [];
        let totalDistanceKm = 0;

        normalizedStops.forEach((stop, index) => {
            if (index === 0) {
                segments.push({
                    SequenceOrder: stop.SequenceOrder,
                    LocationId: stop.LocationId,
                    DistanceKm: 0
                });
                appendRoutePoint(path, stop.Latitude, stop.Longitude);
                return;
            }

            const previousStop = normalizedStops[index - 1];
            const distanceKm = calculateDistanceKm(
                previousStop.Latitude,
                previousStop.Longitude,
                stop.Latitude,
                stop.Longitude);

            totalDistanceKm += distanceKm;
            segments.push({
                SequenceOrder: stop.SequenceOrder,
                LocationId: stop.LocationId,
                DistanceKm: roundDistanceKm(distanceKm)
            });
            appendRoutePoint(path, previousStop.Latitude, previousStop.Longitude);
            appendRoutePoint(path, stop.Latitude, stop.Longitude);
        });

        const roundedDistanceKm = roundDistanceKm(totalDistanceKm);
        const estimatedDurationMinutes = calculateWalkingDurationMinutes(roundedDistanceKm, normalizedWalkingSpeedKph);

        return {
            TotalDistanceKm: roundedDistanceKm,
            EstimatedDurationMinutes: estimatedDurationMinutes,
            WalkingSpeedKph: normalizedWalkingSpeedKph,
            StartTime: normalizedStartTime,
            FinishTime: calculateFinishTime(normalizedStartTime, estimatedDurationMinutes),
            UsesRoadRouting: !!usesRoadRouting,
            Segments: segments,
            Path: path
        };
    };

    const buildRoutingUrl = (stops) => {
        const coordinates = stops
            .map((stop) => `${stop.Longitude},${stop.Latitude}`)
            .join(";");
        const url = new URL(`${routePlanningConfig.baseUrl}/route/v1/${routePlanningConfig.profile}/${coordinates}`);
        url.searchParams.set("alternatives", "false");
        url.searchParams.set("overview", "full");
        url.searchParams.set("steps", "false");
        url.searchParams.set("geometries", "geojson");
        return url;
    };

    const requestRoadRouteAsync = async (stops) => {
        const controller = new AbortController();
        const timeoutId = window.setTimeout(() => controller.abort(), routePlanningConfig.requestTimeoutMs);

        try {
            const response = await fetch(buildRoutingUrl(stops), {
                method: "GET",
                mode: "cors",
                signal: controller.signal,
                headers: {
                    "Accept": "application/json"
                }
            });

            if (!response.ok) {
                return null;
            }

            const payload = await response.json();
            if (!Array.isArray(payload?.routes) || payload.routes.length === 0) {
                return null;
            }

            return [...payload.routes]
                .sort((left, right) => (Number(left?.distance) || Number.MAX_SAFE_INTEGER) - (Number(right?.distance) || Number.MAX_SAFE_INTEGER))[0] ?? null;
        } finally {
            window.clearTimeout(timeoutId);
        }
    };

    const buildRoadRoutePreview = (stops, startTime, walkingSpeedKph, route) => {
        const normalizedStops = normalizeRouteStops(stops);
        if (normalizedStops.length <= 1) {
            return buildStraightLinePreview(normalizedStops, startTime, walkingSpeedKph, true);
        }

        const legs = Array.isArray(route?.legs) ? route.legs : [];
        const coordinates = Array.isArray(route?.geometry?.coordinates) ? route.geometry.coordinates : [];
        if (legs.length !== normalizedStops.length - 1 || coordinates.length === 0) {
            return buildStraightLinePreview(normalizedStops, startTime, walkingSpeedKph, false);
        }

        const path = [];
        coordinates.forEach((coordinate) => {
            if (!Array.isArray(coordinate) || coordinate.length < 2) {
                return;
            }

            appendRoutePoint(path, coordinate[1], coordinate[0]);
        });

        if (path.length === 0) {
            return buildStraightLinePreview(normalizedStops, startTime, walkingSpeedKph, false);
        }

        const segments = [{
            SequenceOrder: normalizedStops[0].SequenceOrder,
            LocationId: normalizedStops[0].LocationId,
            DistanceKm: 0
        }];
        let totalDistanceKm = 0;

        for (let index = 1; index < normalizedStops.length; index += 1) {
            const leg = legs[index - 1];
            const legDistanceKm = Number(leg?.distance) / 1000;
            if (!Number.isFinite(legDistanceKm) || legDistanceKm < 0) {
                return buildStraightLinePreview(normalizedStops, startTime, walkingSpeedKph, false);
            }

            totalDistanceKm += legDistanceKm;
            segments.push({
                SequenceOrder: normalizedStops[index].SequenceOrder,
                LocationId: normalizedStops[index].LocationId,
                DistanceKm: roundDistanceKm(legDistanceKm)
            });
        }

        const roundedDistanceKm = roundDistanceKm(totalDistanceKm);
        const normalizedStartTime = normalizeTimeValue(startTime);
        const normalizedWalkingSpeedKph = Number.isFinite(Number(walkingSpeedKph)) && Number(walkingSpeedKph) > 0
            ? Number(walkingSpeedKph)
            : 5;
        const estimatedDurationMinutes = calculateWalkingDurationMinutes(roundedDistanceKm, normalizedWalkingSpeedKph);

        return {
            TotalDistanceKm: roundedDistanceKm,
            EstimatedDurationMinutes: estimatedDurationMinutes,
            WalkingSpeedKph: normalizedWalkingSpeedKph,
            StartTime: normalizedStartTime,
            FinishTime: calculateFinishTime(normalizedStartTime, estimatedDurationMinutes),
            UsesRoadRouting: true,
            Segments: segments,
            Path: path
        };
    };

    const fitPlannerBounds = (planner, state) => {
        const normalizedPoints = Array.isArray(state?.points) ? state.points : [];
        const routeLatLngs = getSelectedRouteLatLngs(normalizedPoints);
        const routedPathLatLngs = getRoutePathLatLngs(state);
        const allLatLngs = getPlannerLatLngs(normalizedPoints);
        const targetLatLngs = routedPathLatLngs.length
            ? routedPathLatLngs
            : routeLatLngs.length
                ? routeLatLngs
                : allLatLngs;

        if (!targetLatLngs.length) {
            planner.map.setView([defaultMapCenter.lat, defaultMapCenter.lng], 13);
            return;
        }

        if (targetLatLngs.length === 1) {
            planner.map.setView(targetLatLngs[0], 15);
            return;
        }

        planner.map.fitBounds(L.latLngBounds(targetLatLngs).pad(0.2));
    };

    const renderTourPlanner = (planner, state, fitBounds) => {
        const normalizedPoints = Array.isArray(state?.points) ? state.points : [];
        const routePathLatLngs = getRoutePathLatLngs(state);
        const usesRoadRouting = state?.usesRoadRouting !== false;
        planner.layerGroup.clearLayers();

        normalizedPoints.forEach((point) => {
            if (!Number.isFinite(Number(point?.latitude)) || !Number.isFinite(Number(point?.longitude))) {
                return;
            }

            const marker = L.marker([Number(point.latitude), Number(point.longitude)], {
                icon: createTourMarkerIcon(point),
                keyboard: false
            });

            marker.bindPopup(`
                <div class="tour-map-popup">
                    <strong>${escapeHtml(point?.name)}</strong>
                    <div>${escapeHtml(point?.ownerName || "Unassigned owner")}</div>
                    <div>${point?.isSelected ? `Stop ${escapeHtml(point?.order)}` : "Click to add this POI"}</div>
                </div>
            `);

            marker.on("click", () => {
                if (!planner.dotNetRef || !point?.id) {
                    return;
                }

                planner.dotNetRef.invokeMethodAsync("ToggleStopFromMap", Number(point.id)).catch(() => {
                });
            });

            planner.layerGroup.addLayer(marker);
        });

        const routeLatLngs = routePathLatLngs.length
            ? routePathLatLngs
            : getSelectedRouteLatLngs(normalizedPoints);
        if (routeLatLngs.length > 1) {
            planner.layerGroup.addLayer(L.polyline(routeLatLngs, {
                color: "#0f766e",
                weight: 5,
                opacity: 0.88,
                dashArray: usesRoadRouting ? undefined : "10 8"
            }));
        }

        if (fitBounds) {
            fitPlannerBounds(planner, state);
        }

        planner.map.invalidateSize();
    };

    const normalizeStatisticsLatLngs = (points) => (Array.isArray(points) ? points : [])
        .filter((point) =>
            Number.isFinite(Number(point?.latitude))
            && Number.isFinite(Number(point?.longitude)))
        .map((point) => [Number(point.latitude), Number(point.longitude)]);

    const normalizeStatisticsHeatPoints = (points) => (Array.isArray(points) ? points : [])
        .map((point) => ({
            latitude: Number(point?.latitude),
            longitude: Number(point?.longitude),
            sessionCount: Math.max(1, Number(point?.sessionCount) || 1),
            intensity: Math.max(1, Number(point?.intensity) || 1),
            ward: String(point?.ward ?? "").trim()
        }))
        .filter((point) =>
            Number.isFinite(point.latitude)
            && Number.isFinite(point.longitude));

    const getStatisticsHeatTone = (sessionCount) => {
        const normalizedCount = Number(sessionCount) || 0;
        if (normalizedCount > 5) {
            return "red";
        }

        if (normalizedCount > 1) {
            return "yellow";
        }

        return "orange";
    };

    const getStatisticsHeatColor = (sessionCount) => {
        const tone = getStatisticsHeatTone(sessionCount);
        if (tone === "red") {
            return "#ef4444";
        }

        if (tone === "yellow") {
            return "#ffd24a";
        }

        return "#ff9736";
    };

    const getStatisticsHeatSize = (sessionCount) => {
        const normalizedCount = Number(sessionCount) || 0;
        if (normalizedCount > 5) {
            return 58;
        }

        if (normalizedCount > 1) {
            return 50;
        }

        return 42;
    };

    const createStatisticsHeatIcon = (point) => {
        const sessionCount = Math.max(1, Number(point?.sessionCount) || 1);
        const size = getStatisticsHeatSize(sessionCount);
        const tone = getStatisticsHeatTone(sessionCount);
        const label = sessionCount > 99 ? "99+" : String(sessionCount);

        return L.divIcon({
            className: "statistics-map__heat-icon-shell",
            html: `<div class="statistics-map__heat-icon statistics-map__heat-icon--${tone}" style="--heat-size:${size}px;"><span>${escapeHtml(label)}</span></div>`,
            iconSize: [size, size],
            iconAnchor: [size / 2, size / 2]
        });
    };

    const getStatisticsEntryValue = (entry, key, fallbackValue = 0) => {
        const original = entry?.o ?? entry;
        const numericValue = Number(original?.[key]);
        return Number.isFinite(numericValue) ? numericValue : fallbackValue;
    };

    const getStatisticsBinSessionCount = (bin) => bin.reduce((total, entry) =>
        total + Math.max(1, getStatisticsEntryValue(entry, "sessionCount", 1)), 0);

    const getStatisticsBinIntensity = (bin) => bin.reduce((total, entry) =>
        total + Math.max(1, getStatisticsEntryValue(entry, "intensity", 1)), 0);

    const getStatisticsBinWards = (bin) => [...new Set(bin
        .map((entry) => String(entry?.o?.ward ?? entry?.ward ?? "").trim())
        .filter(Boolean))];

    const getStatisticsBinWardLabel = (bin) => {
        const wards = getStatisticsBinWards(bin);
        if (!wards.length) {
            return "Hotspot cluster";
        }

        if (wards.length === 1) {
            return wards[0];
        }

        return `${wards[0]} +${wards.length - 1} more`;
    };

    const createStatisticsHexbinPopupContent = (bin) => {
        const totalUsers = getStatisticsBinSessionCount(bin);
        const totalTrackingPoints = getStatisticsBinIntensity(bin);
        const hotspotCount = Array.isArray(bin) ? bin.length : 0;
        const wardLabel = getStatisticsBinWardLabel(bin);

        return `
            <div class="statistics-map__popup">
                <strong>${escapeHtml(wardLabel)}</strong>
                <div>${escapeHtml(`${totalUsers} user(s) across ${hotspotCount} hotspot cell(s)`)}</div>
                <div>${escapeHtml(`${totalTrackingPoints} tracking point(s) captured`)}</div>
            </div>
        `;
    };

    const supportsStatisticsHexbin = () =>
        typeof L !== "undefined"
        && typeof L.hexbinLayer === "function"
        && typeof window.d3 !== "undefined";

    const redrawStatisticsHexLayer = (statisticsMap) => {
        if (!statisticsMap?.hexLayer || typeof statisticsMap.hexLayer.redraw !== "function") {
            return;
        }

        try {
            statisticsMap.hexLayer.redraw();
        } catch {
        }
    };

    const bindStatisticsHexbinInteractions = (statisticsMap) => {
        if (!statisticsMap?.hexLayer || statisticsMap.hexLayerEventsBound) {
            return;
        }

        const dispatch = statisticsMap.hexLayer.dispatch();
        dispatch.on("click.statistics", (event, bin) => {
            if (!Array.isArray(bin) || !bin.length) {
                return;
            }

            const targetPoint = L.point(bin.x, bin.y);
            const popupLatLng = statisticsMap.map.layerPointToLatLng(targetPoint);

            L.popup({
                offset: [0, -12]
            })
                .setLatLng(popupLatLng)
                .setContent(createStatisticsHexbinPopupContent(bin))
                .openOn(statisticsMap.map);
        });

        statisticsMap.hexLayerEventsBound = true;
    };

    const ensureStatisticsHexLayer = (statisticsMap) => {
        if (!supportsStatisticsHexbin()) {
            return null;
        }

        if (statisticsMap.hexLayer) {
            return statisticsMap.hexLayer;
        }

        const hexLayer = L.hexbinLayer({
            radius: 22,
            opacity: 0.88,
            duration: 220,
            colorScaleExtent: [1, 12],
            radiusScaleExtent: [1, 12],
            radiusRange: [18, 26],
            pointerEvents: "visiblePainted"
        })
            .lng((item) => Number(item?.longitude))
            .lat((item) => Number(item?.latitude))
            .colorValue((bin) => getStatisticsBinSessionCount(bin))
            .radiusValue((bin) => getStatisticsBinSessionCount(bin))
            .fill((bin) => getStatisticsHeatColor(getStatisticsBinSessionCount(bin)))
            .hoverHandler(L.HexbinHoverHandler.resizeScale({
                radiusScale: 0.16
            }));

        hexLayer.addTo(statisticsMap.map);
        statisticsMap.hexLayer = hexLayer;
        bindStatisticsHexbinInteractions(statisticsMap);
        return hexLayer;
    };

    const renderStatisticsHeatFallback = (statisticsMap, heatPoints) => {
        statisticsMap.fallbackHeatLayer.clearLayers();

        heatPoints.forEach((point) => {
            const marker = L.marker([point.latitude, point.longitude], {
                icon: createStatisticsHeatIcon(point),
                keyboard: false,
                zIndexOffset: point.sessionCount > 5 ? 400 : 250
            });

            marker.bindTooltip(`${point.sessionCount} user(s) in this area`, {
                direction: "top",
                offset: [0, -10]
            });

            marker.bindPopup(`
                <div class="statistics-map__popup">
                    <strong>${escapeHtml(point.ward || "Hotspot cluster")}</strong>
                    <div>${escapeHtml(`${point.sessionCount} user(s) in this area`)}</div>
                    <div>${escapeHtml(`${point.intensity} tracking point(s) captured here`)}</div>
                </div>
            `);

            statisticsMap.fallbackHeatLayer.addLayer(marker);
        });
    };

    const getStatisticsBoundsLatLngs = (state) => {
        const routeLatLngs = normalizeStatisticsLatLngs(state?.selectedRoute?.points);
        if (routeLatLngs.length) {
            return routeLatLngs;
        }

        const heatLatLngs = normalizeStatisticsLatLngs(state?.heatPoints);
        const locationLatLngs = normalizeStatisticsLatLngs(state?.locations);
        return [...heatLatLngs, ...locationLatLngs];
    };

    const fitStatisticsBounds = (statisticsMap, state) => {
        const targetLatLngs = getStatisticsBoundsLatLngs(state);
        if (!targetLatLngs.length) {
            statisticsMap.map.setView([defaultMapCenter.lat, defaultMapCenter.lng], 13);
            return;
        }

        if (targetLatLngs.length === 1) {
            statisticsMap.map.setView(targetLatLngs[0], 15);
            return;
        }

        statisticsMap.map.fitBounds(L.latLngBounds(targetLatLngs).pad(0.18));
    };

    const renderStatisticsMap = (statisticsMap, state, fitBounds) => {
        const heatPoints = normalizeStatisticsHeatPoints(state?.heatPoints);
        const locations = Array.isArray(state?.locations) ? state.locations : [];
        const routePoints = Array.isArray(state?.selectedRoute?.points) ? state.selectedRoute.points : [];
        statisticsMap.state = state;

        statisticsMap.routeLayer.clearLayers();
        statisticsMap.poiLayer.clearLayers();
        statisticsMap.fallbackHeatLayer.clearLayers();

        const hexLayer = ensureStatisticsHexLayer(statisticsMap);
        if (hexLayer) {
            hexLayer.data(heatPoints);
        } else {
            renderStatisticsHeatFallback(statisticsMap, heatPoints);
        }

        const routeLatLngs = normalizeStatisticsLatLngs(routePoints);
        if (routeLatLngs.length > 1) {
            statisticsMap.routeLayer.addLayer(L.polyline(routeLatLngs, {
                color: "#0f766e",
                weight: 5,
                opacity: 0.92,
                lineCap: "round",
                lineJoin: "round"
            }));

            statisticsMap.routeLayer.addLayer(L.circleMarker(routeLatLngs[0], {
                radius: 6,
                color: "#ffffff",
                weight: 2,
                fillColor: "#10b981",
                fillOpacity: 1
            }).bindTooltip("Route start"));

            statisticsMap.routeLayer.addLayer(L.circleMarker(routeLatLngs[routeLatLngs.length - 1], {
                radius: 6,
                color: "#ffffff",
                weight: 2,
                fillColor: "#0f172a",
                fillOpacity: 1
            }).bindTooltip("Route end"));
        } else if (routeLatLngs.length === 1) {
            statisticsMap.routeLayer.addLayer(L.circleMarker(routeLatLngs[0], {
                radius: 7,
                color: "#ffffff",
                weight: 2,
                fillColor: "#10b981",
                fillOpacity: 1
            }).bindTooltip("Route point"));
        }

        locations.forEach((location) => {
            const latitude = Number(location?.latitude);
            const longitude = Number(location?.longitude);
            if (!Number.isFinite(latitude) || !Number.isFinite(longitude)) {
                return;
            }

            const marker = L.circleMarker([latitude, longitude], {
                radius: 7,
                color: "#ffffff",
                weight: 3,
                fillColor: "#0f172a",
                fillOpacity: 0.96,
                className: "statistics-map__poi-dot"
            });

            const locationName = String(location?.name ?? "").trim() || "POI";
            const ward = String(location?.ward ?? "").trim() || "Unassigned";
            const ownerName = String(location?.ownerName ?? "").trim() || "No owner";

            marker.bindTooltip(locationName, {
                direction: "top",
                offset: [0, -8]
            });

            marker.bindPopup(`
                <div class="statistics-map__popup">
                    <strong>${escapeHtml(locationName)}</strong>
                    <div>${escapeHtml(ward)}</div>
                    <div>${escapeHtml(ownerName)}</div>
                </div>
            `);

            statisticsMap.poiLayer.addLayer(marker);
        });

        if (fitBounds) {
            fitStatisticsBounds(statisticsMap, state);
        }

        statisticsMap.map.invalidateSize();
        redrawStatisticsHexLayer(statisticsMap);
    };

    const focusStatisticsMapLocation = (statisticsMap, latitude, longitude, title, subtitle, zoomLevel = 16) => {
        const normalizedLatitude = Number(latitude);
        const normalizedLongitude = Number(longitude);
        if (!statisticsMap?.map
            || !Number.isFinite(normalizedLatitude)
            || !Number.isFinite(normalizedLongitude)) {
            return false;
        }

        const latLng = [normalizedLatitude, normalizedLongitude];
        if (!statisticsMap.searchFocusMarker) {
            statisticsMap.searchFocusMarker = L.circleMarker(latLng, {
                radius: 9,
                color: "#ffffff",
                weight: 3,
                fillColor: "#2563eb",
                fillOpacity: 0.96,
                className: "statistics-map__search-focus"
            }).addTo(statisticsMap.map);
        } else {
            statisticsMap.searchFocusMarker.setLatLng(latLng);
        }

        const heading = String(title ?? "").trim() || "Searched place";
        const supportingText = String(subtitle ?? "").trim();
        statisticsMap.searchFocusMarker.bindPopup(`
            <div class="statistics-map__popup">
                <strong>${escapeHtml(heading)}</strong>
                ${supportingText ? `<div>${escapeHtml(supportingText)}</div>` : ""}
                <div>${escapeHtml(`${normalizedLatitude.toFixed(6)}, ${normalizedLongitude.toFixed(6)}`)}</div>
            </div>
        `);

        statisticsMap.map.setView(latLng, Number.isFinite(Number(zoomLevel)) ? Number(zoomLevel) : 16);
        statisticsMap.searchFocusMarker.openPopup();
        return true;
    };

    const updateStatisticsCurrentLocation = (statisticsMap, latitude, longitude, accuracyMeters, shouldCenter) => {
        const normalizedLatitude = Number(latitude);
        const normalizedLongitude = Number(longitude);
        if (!statisticsMap?.map
            || !Number.isFinite(normalizedLatitude)
            || !Number.isFinite(normalizedLongitude)) {
            return false;
        }

        const latLng = [normalizedLatitude, normalizedLongitude];
        if (!statisticsMap.currentLocationMarker) {
            statisticsMap.currentLocationMarker = L.circleMarker(latLng, {
                radius: 8,
                color: "#ffffff",
                weight: 3,
                fillColor: "#0ea5e9",
                fillOpacity: 1,
                className: "statistics-map__current-focus"
            }).addTo(statisticsMap.map);
        } else {
            statisticsMap.currentLocationMarker.setLatLng(latLng);
        }

        statisticsMap.currentLocationMarker.bindPopup(`
            <div class="statistics-map__popup">
                <strong>Current device location</strong>
                <div>${escapeHtml(`${normalizedLatitude.toFixed(6)}, ${normalizedLongitude.toFixed(6)}`)}</div>
            </div>
        `);

        const normalizedAccuracy = Math.max(0, Number(accuracyMeters) || 0);
        if (normalizedAccuracy > 0) {
            if (!statisticsMap.currentLocationAccuracyCircle) {
                statisticsMap.currentLocationAccuracyCircle = L.circle(latLng, {
                    radius: normalizedAccuracy,
                    color: "#0ea5e9",
                    weight: 1.5,
                    fillColor: "#38bdf8",
                    fillOpacity: 0.12,
                    interactive: false
                }).addTo(statisticsMap.map);
            } else {
                statisticsMap.currentLocationAccuracyCircle.setLatLng(latLng);
                statisticsMap.currentLocationAccuracyCircle.setRadius(normalizedAccuracy);
            }
        } else if (statisticsMap.currentLocationAccuracyCircle) {
            statisticsMap.map.removeLayer(statisticsMap.currentLocationAccuracyCircle);
            statisticsMap.currentLocationAccuracyCircle = null;
        }

        if (shouldCenter) {
            statisticsMap.map.setView(latLng, Math.max(statisticsMap.map.getZoom(), 15));
            statisticsMap.currentLocationMarker.openPopup();
        }

        return true;
    };

    const stopStatisticsMapTracking = (statisticsMap, removeMarker) => {
        if (!statisticsMap) {
            return;
        }

        if (statisticsMap.trackingWatchId != null && "geolocation" in navigator) {
            navigator.geolocation.clearWatch(statisticsMap.trackingWatchId);
        }

        statisticsMap.trackingWatchId = null;
        statisticsMap.hasCenteredOnCurrentLocation = false;

        if (!removeMarker || !statisticsMap.map) {
            return;
        }

        if (statisticsMap.currentLocationMarker) {
            statisticsMap.map.removeLayer(statisticsMap.currentLocationMarker);
            statisticsMap.currentLocationMarker = null;
        }

        if (statisticsMap.currentLocationAccuracyCircle) {
            statisticsMap.map.removeLayer(statisticsMap.currentLocationAccuracyCircle);
            statisticsMap.currentLocationAccuracyCircle = null;
        }
    };

    const startStatisticsMapTrackingAsync = async (statisticsMap) => await new Promise((resolve) => {
        if (!statisticsMap?.map || !("geolocation" in navigator)) {
            resolve(false);
            return;
        }

        if (statisticsMap.trackingWatchId != null) {
            resolve(true);
            return;
        }

        let settled = false;
        const complete = (value) => {
            if (settled) {
                return;
            }

            settled = true;
            resolve(value);
        };

        statisticsMap.hasCenteredOnCurrentLocation = false;
        statisticsMap.trackingWatchId = navigator.geolocation.watchPosition((position) => {
            updateStatisticsCurrentLocation(
                statisticsMap,
                position.coords.latitude,
                position.coords.longitude,
                position.coords.accuracy,
                !statisticsMap.hasCenteredOnCurrentLocation);

            statisticsMap.hasCenteredOnCurrentLocation = true;
            complete(true);
        }, () => {
            stopStatisticsMapTracking(statisticsMap, true);
            complete(false);
        }, {
            enableHighAccuracy: true,
            timeout: 10000,
            maximumAge: 30000
        });
    });

    const delayAsync = (durationMs) => new Promise((resolve) => {
        window.setTimeout(resolve, durationMs);
    });

    const hasUsableMapSize = (element) => {
        if (!element) {
            return false;
        }

        const bounds = element.getBoundingClientRect();
        return bounds.width > 0 && bounds.height > 0;
    };

    const waitForMapSizeAsync = async (element, maxAttempts = 14, delayMs = 120) => {
        for (let attempt = 0; attempt < maxAttempts; attempt += 1) {
            if (hasUsableMapSize(element)) {
                return true;
            }

            await delayAsync(delayMs);
        }

        return hasUsableMapSize(element);
    };

    const invalidateLeafletMap = (map, afterInvalidate) => {
        if (!map) {
            return;
        }

        const invalidate = () => {
            try {
                map.invalidateSize();
            } catch {
            }

            if (typeof afterInvalidate === "function") {
                try {
                    afterInvalidate();
                } catch {
                }
            }
        };

        invalidate();
        window.requestAnimationFrame(invalidate);
        window.setTimeout(invalidate, 120);
        window.setTimeout(invalidate, 420);
    };

    const attachMapResizeHandling = (element, map, afterInvalidate) => {
        const handleResize = () => invalidateLeafletMap(map, afterInvalidate);
        window.addEventListener("resize", handleResize);

        let resizeObserver = null;
        if ("ResizeObserver" in window && element) {
            resizeObserver = new ResizeObserver(() => invalidateLeafletMap(map, afterInvalidate));
            resizeObserver.observe(element);
        }

        return {
            handleResize,
            resizeObserver
        };
    };

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
        searchLocationPicker: async (query) => {
            return await searchLocationMatches(query);
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
        },
        initializeTourPlanner: (element, dotNetRef, points) => {
            if (!element || typeof L === "undefined") {
                return false;
            }

            const existingPlanner = tourPlanners.get(element);
            if (existingPlanner) {
                existingPlanner.dotNetRef = dotNetRef;
                renderTourPlanner(existingPlanner, points, true);
                return true;
            }

            const map = L.map(element, {
                zoomControl: true
            });

            L.tileLayer("https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png", {
                maxZoom: 19,
                attribution: "&copy; OpenStreetMap contributors"
            }).addTo(map);

            const planner = {
                dotNetRef,
                map,
                layerGroup: L.layerGroup().addTo(map)
            };

            renderTourPlanner(planner, points, true);
            tourPlanners.set(element, planner);

            window.setTimeout(() => {
                map.invalidateSize();
            }, 0);

            return true;
        },
        syncTourPlanner: (element, points) => {
            const planner = tourPlanners.get(element);
            if (!planner) {
                return false;
            }

            renderTourPlanner(planner, points, true);
            return true;
        },
        calculateTourRoute: async (stops, startTime, walkingSpeedKph) => {
            const normalizedStops = normalizeRouteStops(stops);
            if (normalizedStops.length <= 1) {
                return buildStraightLinePreview(normalizedStops, startTime, walkingSpeedKph, true);
            }

            try {
                const route = await requestRoadRouteAsync(normalizedStops);
                if (!route) {
                    return buildStraightLinePreview(normalizedStops, startTime, walkingSpeedKph, false);
                }

                return buildRoadRoutePreview(normalizedStops, startTime, walkingSpeedKph, route);
            } catch {
                return buildStraightLinePreview(normalizedStops, startTime, walkingSpeedKph, false);
            }
        },
        disposeTourPlanner: (element) => {
            const planner = tourPlanners.get(element);
            if (!planner) {
                return;
            }

            planner.map.remove();
            tourPlanners.delete(element);
        },
        initializeStatisticsMap: async (element, state) => {
            if (!element || typeof L === "undefined") {
                return false;
            }

            const hasSize = await waitForMapSizeAsync(element);
            if (!hasSize) {
                return false;
            }

            const existingMap = statisticsMaps.get(element);
            if (existingMap) {
                renderStatisticsMap(existingMap, state, true);
                invalidateLeafletMap(existingMap.map, () => redrawStatisticsHexLayer(existingMap));
                return true;
            }

            const map = L.map(element, {
                zoomControl: true
            });

            L.tileLayer("https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png", {
                maxZoom: 19,
                attribution: "&copy; OpenStreetMap contributors"
            }).addTo(map);

            const statisticsMap = {
                element,
                map,
                state,
                hexLayer: null,
                hexLayerEventsBound: false,
                fallbackHeatLayer: null,
                routeLayer: null,
                poiLayer: null,
                searchFocusMarker: null,
                currentLocationMarker: null,
                currentLocationAccuracyCircle: null,
                trackingWatchId: null,
                hasCenteredOnCurrentLocation: false
            };

            ensureStatisticsHexLayer(statisticsMap);
            statisticsMap.fallbackHeatLayer = L.layerGroup().addTo(map);
            statisticsMap.routeLayer = L.layerGroup().addTo(map);
            statisticsMap.poiLayer = L.layerGroup().addTo(map);

            const resizeHandling = attachMapResizeHandling(element, map, () => redrawStatisticsHexLayer(statisticsMap));
            statisticsMap.handleResize = resizeHandling.handleResize;
            statisticsMap.resizeObserver = resizeHandling.resizeObserver;

            renderStatisticsMap(statisticsMap, state, true);
            statisticsMaps.set(element, statisticsMap);
            invalidateLeafletMap(map, () => redrawStatisticsHexLayer(statisticsMap));

            return true;
        },
        syncStatisticsMap: async (element, state) => {
            const statisticsMap = statisticsMaps.get(element);
            if (!statisticsMap) {
                return false;
            }

            await waitForMapSizeAsync(element, 8, 90);
            renderStatisticsMap(statisticsMap, state, true);
            invalidateLeafletMap(statisticsMap.map, () => redrawStatisticsHexLayer(statisticsMap));
            return true;
        },
        invalidateStatisticsMap: async (element) => {
            const statisticsMap = statisticsMaps.get(element);
            if (!statisticsMap) {
                return false;
            }

            await waitForMapSizeAsync(element, 8, 90);
            invalidateLeafletMap(statisticsMap.map, () => redrawStatisticsHexLayer(statisticsMap));
            return true;
        },
        focusStatisticsMapLocation: (element, latitude, longitude, title, subtitle) => {
            const statisticsMap = statisticsMaps.get(element);
            if (!statisticsMap) {
                return false;
            }

            return focusStatisticsMapLocation(statisticsMap, latitude, longitude, title, subtitle, 16);
        },
        startStatisticsMapTracking: async (element) => {
            const statisticsMap = statisticsMaps.get(element);
            if (!statisticsMap) {
                return false;
            }

            await waitForMapSizeAsync(element, 8, 90);
            return await startStatisticsMapTrackingAsync(statisticsMap);
        },
        stopStatisticsMapTracking: (element) => {
            const statisticsMap = statisticsMaps.get(element);
            if (!statisticsMap) {
                return false;
            }

            stopStatisticsMapTracking(statisticsMap, true);
            return true;
        },
        disposeStatisticsMap: (element) => {
            const statisticsMap = statisticsMaps.get(element);
            if (!statisticsMap) {
                return;
            }

            if (statisticsMap.resizeObserver) {
                statisticsMap.resizeObserver.disconnect();
            }

            if (statisticsMap.handleResize) {
                window.removeEventListener("resize", statisticsMap.handleResize);
            }

            stopStatisticsMapTracking(statisticsMap, true);
            statisticsMap.map.remove();
            statisticsMaps.delete(element);
        }
    };

    const seen = new WeakSet();
    const trackedScrollContainers = new WeakSet();
    let observer = null;
    let scrollDirection = "down";

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

    const setScrollDirection = (nextDirection) => {
        if (nextDirection !== "up" && nextDirection !== "down") {
            return;
        }

        scrollDirection = nextDirection;
        root.dataset.scrollDirection = nextDirection;
    };

    const bindScrollTracking = (target, isWindow = false) => {
        if (!target || trackedScrollContainers.has(target)) {
            return;
        }

        trackedScrollContainers.add(target);

        let lastPosition = isWindow
            ? window.scrollY
            : Number(target.scrollTop ?? 0);

        const handleScroll = () => {
            const currentPosition = isWindow
                ? window.scrollY
                : Number(target.scrollTop ?? 0);

            if (Math.abs(currentPosition - lastPosition) < 6) {
                return;
            }

            setScrollDirection(currentPosition > lastPosition ? "down" : "up");
            lastPosition = currentPosition;
        };

        const eventTarget = isWindow ? window : target;
        eventTarget.addEventListener("scroll", handleScroll, { passive: true });
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
                        const scrollContainer = node.matches(".admin-shell__content")
                            ? node
                            : node.querySelector?.(".admin-shell__content");
                        if (scrollContainer instanceof HTMLElement) {
                            bindScrollTracking(scrollContainer);
                        }

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
        bindScrollTracking(window, true);

        const content = document.querySelector(".admin-shell__content");
        if (content instanceof HTMLElement) {
            bindScrollTracking(content);
        }

        activate(document);
        observeMutations();
    };

    if (document.readyState === "loading") {
        document.addEventListener("DOMContentLoaded", start, { once: true });
    } else {
        start();
    }
})();
