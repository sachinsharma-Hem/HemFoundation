// document.addEventListener("DOMContentLoaded", function() {
//     const heroSection = document.querySelector('.hero-section');
//     const cards = document.querySelectorAll('.value-card');

//     if (window.innerWidth > 991) {
//         heroSection.addEventListener('mousemove', (e) => {
//             const { width, height } = heroSection.getBoundingClientRect();
//             const offsetX = (e.clientX / width - 0.5) * 25;
//             const offsetY = (e.clientY / height - 0.5) * 25;

//             cards.forEach((card, index) => {
//                 const speed = (index + 1) * 0.18;
//                 const x = offsetX * speed;
//                 const y = offsetY * speed;

//                 // Ye aage-peeche position ko bina tode smoothly float karwayega
//                 card.style.transform = `translate(${x}px, ${y}px)`;
//             });
//         });

//         heroSection.addEventListener('mouseleave', () => {
//         cards.forEach(card => {
//             card.style.transform = `translate(0px, 0px)`;
//         });
//         });
//     }
//     });

document.addEventListener("DOMContentLoaded", function () {
    const heroSection = document.querySelector('.hero-section');
    const cards = document.querySelectorAll('.value-card');

    if (heroSection && cards.length > 0) {
        // Run parallax only on desktop screens
        if (window.innerWidth > 991) {
            heroSection.addEventListener('mousemove', (e) => {
                const { width, height } = heroSection.getBoundingClientRect();
                // Smooth multiplier calculation
                const offsetX = (e.clientX / width - 0.5) * 20;
                const offsetY = (e.clientY / height - 0.5) * 20;

                cards.forEach((card, index) => {
                    const speed = (index + 1) * 0.15;
                    const x = offsetX * speed;
                    const y = offsetY * speed;

                    // Applies 3D movement cleanly without breaking CSS keyframes
                    card.style.transform = `translate3d(${x}px, ${y}px, 0)`;
                });
            });

            // Smoothly snap back to original wave position on mouse leave
            heroSection.addEventListener('mouseleave', () => {
                cards.forEach(card => {
                    card.style.transform = `translate3d(0px, 0px, 0)`;
                });
            });
        }
    }
});

document.addEventListener("DOMContentLoaded", () => {
    const hemCounters = document.querySelectorAll(".hem-counter");

    if (hemCounters.length > 0) {
        const speed = 80; // Speed of counting

        const startCounting = (counter) => {
            const target = +counter.getAttribute("data-target");
            let count = 0;
            const inc = target / speed;

            const updateCount = () => {
                count += inc;
                if (count < target) {
                    counter.innerText = Math.ceil(count).toLocaleString('en-US');
                    setTimeout(updateCount, 20);
                } else {
                    counter.innerText = target.toLocaleString('en-US');
                }
            };
            updateCount();
        };

        const observer = new IntersectionObserver((entries, observer) => {
            entries.forEach(entry => {
                if (entry.isIntersecting) {
                    startCounting(entry.target);
                    observer.unobserve(entry.target); // Run only once
                }
            });
        }, { threshold: 0.3 });

        hemCounters.forEach(counter => observer.observe(counter));
    }
});

document.addEventListener("DOMContentLoaded", () => {
    const whyCards = document.querySelectorAll(".hem-why-card");

    if (whyCards.length > 0) {
        whyCards.forEach((card, index) => {
            card.style.opacity = "0";
            card.style.transform = "translateY(45px) scale(0.85)";
            card.style.transition = `opacity 0.7s ease, transform 0.7s cubic-bezier(0.175, 0.885, 0.32, 1.275) ${index * 0.12}s`;
        });

        const observer = new IntersectionObserver((entries, observer) => {
            entries.forEach(entry => {
                if (entry.isIntersecting) {
                    entry.target.style.opacity = "1";
                    entry.target.style.transform = "translateY(0) scale(1)";

                    setTimeout(() => {
                        entry.target.style.transition = "all 0.4s cubic-bezier(0.175, 0.885, 0.32, 1.275)";
                    }, 1200);

                    observer.unobserve(entry.target);
                }
            });
        }, {
            threshold: 0.15, 
            rootMargin: "0px 0px -30px 0px" 
        });

        whyCards.forEach(card => observer.observe(card));
    }
});

document.addEventListener("DOMContentLoaded", () => {
    const howElements = document.querySelectorAll(".hem-how-step-col, .hem-how-arrow");
    const howHeader = document.querySelector(".hem-how-header");

    if (howElements.length > 0) {
        if (howHeader) {
            howHeader.style.opacity = "0";
            howHeader.style.transform = "translateY(-20px)";
            howHeader.style.transition = "all 0.6s ease-out";
        }

        howElements.forEach((el) => {
            el.style.opacity = "0";
            el.style.transform = "translateY(30px) scale(0.95)";
        });

        const observer = new IntersectionObserver((entries, observer) => {
            entries.forEach(entry => {
                if (entry.isIntersecting) {

                    if (howHeader) {
                        howHeader.style.opacity = "1";
                        howHeader.style.transform = "translateY(0)";
                    }

                    howElements.forEach((el, index) => {
                        setTimeout(() => {
                            el.style.transition = "all 0.5s cubic-bezier(0.16, 1, 0.3, 1)";
                            el.style.opacity = "1";
                            el.style.transform = "translateY(0) scale(1)";
                        }, index * 150); 
                    });

                    observer.unobserve(entry.target);
                }
            });
        }, { threshold: 0.2 }); 

        observer.observe(howElements[0]);
    }
});

document.addEventListener("DOMContentLoaded", () => {
    const progCols = document.querySelectorAll(".hem-prog-col");
    const progHeader = document.querySelector(".hem-prog-header");

    if (progCols.length > 0) {
        if (progHeader) {
            progHeader.style.opacity = "0";
            progHeader.style.transform = "translateY(-20px)";
            progHeader.style.transition = "all 0.6s ease-out";
        }

        progCols.forEach((col, index) => {
            col.style.opacity = "0";
            col.style.transform = "translateY(45px) scale(0.85)";
            col.style.transition = `opacity 0.7s ease, transform 0.7s cubic-bezier(0.175, 0.885, 0.32, 1.275) ${index * 0.1}s`;
        });

        const observer = new IntersectionObserver((entries, observer) => {
            entries.forEach(entry => {
                if (entry.isIntersecting) {

                    if (progHeader) {
                        progHeader.style.opacity = "1";
                        progHeader.style.transform = "translateY(0)";
                    }

                    progCols.forEach((col) => {
                        col.style.opacity = "1";
                        col.style.transform = "translateY(0) scale(1)";

                        setTimeout(() => {
                            col.style.transition = "all 0.4s cubic-bezier(0.175, 0.885, 0.32, 1.275)";
                        }, 1200);
                    });

                    observer.unobserve(entry.target);
                }
            });
        }, {
            threshold: 0.15, 
            rootMargin: "0px 0px -20px 0px"
        });

      
        observer.observe(progCols[0]);
    }
});

document.addEventListener("DOMContentLoaded", () => {
    // 1. Entry Animation for Video Banner
    const vidCols = document.querySelectorAll(".hem-vid-col");

    if (vidCols.length > 0) {
        // On phones the two columns are stacked, so they slide up instead of sideways
        // (a sideways start pushed the text past the right edge of the screen).
        const stacked = window.matchMedia("(max-width: 991.98px)").matches;

        vidCols.forEach((col, index) => {
            col.style.opacity = "0";
            col.style.transform = stacked
                ? "translateY(24px)"
                : (index === 0 ? "translateX(-40px) scale(0.95)" : "translateX(40px) scale(0.95)");
            col.style.transition = `all 0.8s cubic-bezier(0.16, 1, 0.3, 1) ${stacked ? 0 : index * 0.15}s`;
        });

        // Each column appears when it scrolls into view.
        const observer = new IntersectionObserver((entries, observer) => {
            entries.forEach(entry => {
                if (entry.isIntersecting) {
                    entry.target.style.opacity = "1";
                    entry.target.style.transform = "none";
                    observer.unobserve(entry.target);
                }
            });
        }, { threshold: 0.15 });

        vidCols.forEach(col => observer.observe(col));
    }

    // 2. Smart Video Modal Autoplay & Cleanup
    const videoModal = document.getElementById("hemVideoModal");
    const videoContainer = document.getElementById("videoContainer");

    if (videoModal && videoContainer) {
        // Embed URL of your youtube video with autoplay enabled
        // playsinline: phones play it inside the popup instead of forcing full screen.
        const embedUrl = "https://www.youtube.com/embed/_KjRB1egRqk?autoplay=1&rel=0&playsinline=1&modestbranding=1";

        // When modal opens, inject iframe. YouTube needs the page's origin as referrer,
        // otherwise some browsers show "Video unavailable / error 153".
        videoModal.addEventListener("show.bs.modal", () => {
            videoContainer.innerHTML = `<iframe src="${embedUrl}" title="HEMA Foundation: Introduction" allow="accelerometer; autoplay; clipboard-write; encrypted-media; gyroscope; picture-in-picture; web-share" referrerpolicy="strict-origin-when-cross-origin" allowfullscreen class="w-100 h-100 border-0"></iframe>`;
        });

        // When modal closes, remove iframe to stop audio/video
        videoModal.addEventListener("hidden.bs.modal", () => {
            videoContainer.innerHTML = "";
        });
    }
});

document.addEventListener("DOMContentLoaded", () => {
    // 1. Modal Dynamic Data Binding (With Automatic Path Fix)
    const readMoreButtons = document.querySelectorAll(".read-more");

    readMoreButtons.forEach(button => {
        button.addEventListener("click", function () {
            const name = this.getAttribute("data-name");
            const role = this.getAttribute("data-role");
            let img = this.getAttribute("data-img") || "";
            const text = this.getAttribute("data-text");

            // Fix: Ye line path ke aage se tilde (~) aur extra slash ko hata kar sahi web path banayeigi
            img = img.replace(/^(\/?~)/, '');

            const tmName = document.getElementById("tmName");
            const tmRole = document.getElementById("tmRole");
            const tmImg = document.getElementById("tmImg");
            const tmText = document.getElementById("tmText");

            if (tmName) tmName.innerText = name || "";
            if (tmRole) tmRole.innerText = role || "";
            if (tmImg) tmImg.src = img;
            if (tmText) tmText.innerText = text || "";
        });
    });

    // 2. Viewport Entry Animation for Cards (Fixed for First Load & Refresh)
    const hvtCols = document.querySelectorAll(".hvt-col");
    const hvtHeader = document.querySelector(".hvt-header");

    if (hvtCols.length > 0) {
        if (hvtHeader) {
            hvtHeader.style.opacity = "0";
            hvtHeader.style.transform = "translateY(-20px)";
            hvtHeader.style.transition = "all 0.6s ease-out";
        }

        hvtCols.forEach((col, index) => {
            col.style.opacity = "0";
            col.style.transform = "translateY(40px) scale(0.92)";
            col.style.transition = `all 0.7s cubic-bezier(0.175, 0.885, 0.32, 1.275) ${index * 0.15}s`;
        });

        let isAnimated = false;

        // Function to reveal cards
        const showCards = () => {
            if (isAnimated) return;
            isAnimated = true;

            if (hvtHeader) {
                hvtHeader.style.opacity = "1";
                hvtHeader.style.transform = "translateY(0)";
            }

            hvtCols.forEach(col => {
                col.style.opacity = "1";
                col.style.transform = "translateY(0) scale(1)";

                setTimeout(() => {
                    col.style.transition = "all 0.4s cubic-bezier(0.175, 0.885, 0.32, 1.275)";
                }, 1200);
            });
        };

        // Scroll Observer
        const observer = new IntersectionObserver((entries, observer) => {
            entries.forEach(entry => {
                if (entry.isIntersecting) {
                    showCards();
                    observer.unobserve(entry.target);
                }
            });
        }, {
            threshold: 0.1,
            rootMargin: "0px 0px 50px 0px" // Screen me aane se 50px pehle hi trigger hoga
        });

        observer.observe(hvtCols[0]);

        // Safety Fallback: Agar page load pe scroll nahi hua ya slow Internet hai,
        // to 800ms (0.8s) baad apne aap cards show ho jaayenge bina kisi issue ke!
        setTimeout(() => {
            showCards();
        }, 800);
    }
});

document.addEventListener("DOMContentLoaded", () => {
    // 1. Automatic Marquee Cloner for 100% Seamless Infinite Loop
    const partnerTrack = document.getElementById("partnerTrack");

    if (partnerTrack && !partnerTrack.getAttribute("data-cloned")) {
        // Clone all logo cards and append them to the same track
        const cards = Array.from(partnerTrack.children);
        cards.forEach(card => {
            const clone = card.cloneNode(true);
            partnerTrack.appendChild(clone);
        });
        // Mark as cloned to prevent double cloning on re-renders
        partnerTrack.setAttribute("data-cloned", "true");
    }

    // 2. Viewport Entry Animation
    const partnerHeader = document.querySelector(".hem-partner-header");
    const partnerSlider = document.querySelector(".hem-partner-slider");

    if (partnerHeader && partnerSlider) {
        partnerHeader.style.opacity = "0";
        partnerHeader.style.transform = "translateY(-20px)";
        partnerHeader.style.transition = "all 0.6s ease-out";

        partnerSlider.style.opacity = "0";
        partnerSlider.style.transform = "translateY(30px) scale(0.96)";
        partnerSlider.style.transition = "all 0.8s cubic-bezier(0.16, 1, 0.3, 1) 0.2s";

        const observer = new IntersectionObserver((entries, observer) => {
            entries.forEach(entry => {
                if (entry.isIntersecting) {
                    partnerHeader.style.opacity = "1";
                    partnerHeader.style.transform = "translateY(0)";

                    partnerSlider.style.opacity = "1";
                    partnerSlider.style.transform = "translateY(0) scale(1)";

                    observer.unobserve(entry.target);
                }
            });
        }, { threshold: 0.15 });

        observer.observe(partnerHeader);
    }
});

document.addEventListener("DOMContentLoaded", () => {
    const phoneCol = document.querySelector(".hem-app-phone-col");
    const appCols = document.querySelectorAll(".hem-app-col");

    if (phoneCol && appCols.length > 0) {
        // Initial setup for Phone
        phoneCol.style.opacity = "0";
        phoneCol.style.transform = "translateX(-50px)";
        phoneCol.style.transition = "all 0.8s cubic-bezier(0.16, 1, 0.3, 1)";

        // Initial setup for Feature Cards
        appCols.forEach((col, index) => {
            col.style.opacity = "0";
            col.style.transform = "translateY(40px) scale(0.9)";
            col.style.transition = `all 0.6s cubic-bezier(0.175, 0.885, 0.32, 1.275) ${index * 0.12}s`;
        });

        const observer = new IntersectionObserver((entries, observer) => {
            entries.forEach(entry => {
                if (entry.isIntersecting) {
                    // Animate Phone
                    phoneCol.style.opacity = "1";
                    phoneCol.style.transform = "translateX(0)";

                    // Animate Cards
                    appCols.forEach(col => {
                        col.style.opacity = "1";
                        col.style.transform = "translateY(0) scale(1)";
                    });

                    observer.unobserve(entry.target);
                }
            });
        }, { threshold: 0.2 });

        observer.observe(phoneCol);
    }
});

document.addEventListener("DOMContentLoaded", () => {
    // 1. Viewport Entry Animation for CTA Banner
    const ctaImgCol = document.querySelector(".hem-cta-img-col");
    const ctaTextCol = document.querySelector(".hem-cta-text-col");
    const ctaBtnCol = document.querySelector(".hem-cta-btn-col");

    if (ctaImgCol && ctaTextCol && ctaBtnCol) {
        ctaImgCol.style.opacity = "0";
        ctaImgCol.style.transform = "translateX(-40px)";
        ctaImgCol.style.transition = "all 0.8s cubic-bezier(0.16, 1, 0.3, 1)";

        ctaTextCol.style.opacity = "0";
        ctaTextCol.style.transform = "translateY(40px)";
        ctaTextCol.style.transition = "all 0.8s cubic-bezier(0.16, 1, 0.3, 1) 0.15s";

        ctaBtnCol.style.opacity = "0";
        ctaBtnCol.style.transform = "translateX(40px)";
        ctaBtnCol.style.transition = "all 0.8s cubic-bezier(0.16, 1, 0.3, 1) 0.3s";

        const observer = new IntersectionObserver((entries, observer) => {
            entries.forEach(entry => {
                if (entry.isIntersecting) {
                    ctaImgCol.style.opacity = "1";
                    ctaImgCol.style.transform = "translateX(0)";

                    ctaTextCol.style.opacity = "1";
                    ctaTextCol.style.transform = "translateY(0)";

                    ctaBtnCol.style.opacity = "1";
                    ctaBtnCol.style.transform = "translateX(0)";

                    observer.unobserve(entry.target);
                }
            });
        }, { threshold: 0.2 });

        observer.observe(ctaTextCol);
    }

    // 2. Demo Form Submission Handler: sends the request to the admin e-mail (api/demo-request).
    const demoForm = document.getElementById("hemDemoForm");
    if (demoForm) {
        const errorBox = document.getElementById("demoError");
        const submitBtn = document.getElementById("demoSubmit");
        const submitHtml = submitBtn ? submitBtn.innerHTML : "";

        const showError = (message) => {
            if (!errorBox) { alert(message); return; }
            errorBox.querySelector(".alert").textContent = message;
            errorBox.classList.toggle("d-none", !message);
        };

        demoForm.addEventListener("submit", function (e) {
            e.preventDefault();
            showError("");

            if (!demoForm.checkValidity()) {
                demoForm.classList.add("was-validated");
                const first = demoForm.querySelector(":invalid");
                if (first) first.focus();
                return;
            }

            const value = (id) => (document.getElementById(id)?.value || "").trim();
            const payload = {
                name: value("demoName"),
                email: value("demoEmail"),
                phone: value("demoPhone"),
                school: value("demoSchool"),
                message: value("demoMessage"),
                website: value("demoWebsite")
            };

            if (submitBtn) {
                submitBtn.disabled = true;
                submitBtn.innerHTML = '<span class="spinner-border spinner-border-sm" role="status" aria-hidden="true"></span><span>Sending…</span>';
            }

            fetch("/api/demo-request", {
                method: "POST",
                headers: { "Content-Type": "application/json", "X-Requested-With": "XMLHttpRequest" },
                body: JSON.stringify(payload)
            })
                .then(r => r.json().catch(() => ({})).then(j => ({ ok: r.ok, body: j })))
                .then(({ ok, body }) => {
                    if (!ok || body.success === false) {
                        showError(body.message || "We could not send your request. Please try again.");
                        return;
                    }

                    const text = body.message || "Thank you for scheduling a live demo. Our value education team will contact you within 24 hours.";
                    const confirm = () => {
                        if (typeof Swal !== "undefined") {
                            Swal.fire({ title: "Request Received!", text: text, icon: "success", confirmButtonColor: "#0066FF", confirmButtonText: "Great, Thanks!" });
                        } else {
                            alert(text);
                        }
                    };

                    // Close the form first, then confirm (a browser alert would freeze the closing popup).
                    const modalEl = document.getElementById("modalcontact");
                    const modalInstance = modalEl ? bootstrap.Modal.getInstance(modalEl) : null;
                    if (modalInstance && modalEl.classList.contains("show")) {
                        modalEl.addEventListener("hidden.bs.modal", confirm, { once: true });
                        modalInstance.hide();
                    } else {
                        confirm();
                    }

                    demoForm.reset();
                    demoForm.classList.remove("was-validated");
                })
                .catch(() => showError("No internet connection. Please check and try again."))
                .finally(() => {
                    if (submitBtn) { submitBtn.disabled = false; submitBtn.innerHTML = submitHtml; }
                });
        });
    }
});

// "Choose another account type" on the registration page links to /?register=1:
// open the account-type popup straight away.
document.addEventListener("DOMContentLoaded", () => {
    try {
        if (new URLSearchParams(window.location.search).get("register") === "1") {
            const el = document.getElementById("modalRegister");
            if (el && window.bootstrap) bootstrap.Modal.getOrCreateInstance(el).show();
        }
    } catch (e) { /* ignore */ }
});
