(() => {
    const root = document.documentElement;
    root.classList.add("js-ready");

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
