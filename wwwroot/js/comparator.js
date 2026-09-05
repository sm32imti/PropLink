/**
 * PropLink Property Comparator Client-Side Controller
 * Manages comparison selections, localStorage persistence, floating dock, and modal quick-add.
 */

(function () {
    const STORAGE_KEY = 'proplink_compare_list';
    const MAX_COMPARE = 5;

    // Retrieve compare list from localStorage
    function getCompareList() {
        try {
            const raw = localStorage.getItem(STORAGE_KEY);
            return raw ? JSON.parse(raw) : [];
        } catch (e) {
            console.error('Error reading compare list from localStorage', e);
            return [];
        }
    }

    // Save compare list to localStorage
    function saveCompareList(list) {
        try {
            localStorage.setItem(STORAGE_KEY, JSON.stringify(list));
        } catch (e) {
            console.error('Error saving compare list to localStorage', e);
        }
    }

    // Check if ID is in compare list
    function isCompared(id) {
        if (!id) return false;
        const list = getCompareList();
        return list.some(item => item.id.toLowerCase() === id.toString().toLowerCase());
    }

    // Add item to compare list
    function addCompareItem(item) {
        if (!item || !item.id) return false;
        const list = getCompareList();

        if (list.some(i => i.id.toLowerCase() === item.id.toString().toLowerCase())) {
            return false;
        }

        if (list.length >= MAX_COMPARE) {
            showToast(`You can compare up to ${MAX_COMPARE} properties simultaneously. Please remove one first.`, 'warning');
            return false;
        }

        list.push({
            id: item.id,
            title: item.title || 'Verified Property',
            price: item.price || '',
            imageUrl: item.imageUrl || '',
            propertyType: item.propertyType || '',
            city: item.city || ''
        });

        saveCompareList(list);
        updateAllUI();
        showToast(`Added "${item.title || 'Property'}" to comparison list.`, 'success');
        return true;
    }

    // Remove item from compare list
    function removeCompareItem(id) {
        if (!id) return;
        let list = getCompareList();
        list = list.filter(i => i.id.toLowerCase() !== id.toString().toLowerCase());
        saveCompareList(list);
        updateAllUI();

        // If currently on compare page, refresh comparison view
        if (window.location.pathname.toLowerCase().includes('/compare')) {
            const currentIds = list.map(i => i.id).join(',');
            window.location.href = `/properties/compare${currentIds ? '?ids=' + currentIds : ''}`;
        }
    }

    // Clear entire compare list
    function clearCompare() {
        saveCompareList([]);
        updateAllUI();
        showToast('Comparison list cleared.', 'info');

        // If on compare page, refresh
        if (window.location.pathname.toLowerCase().includes('/compare')) {
            window.location.href = '/properties/compare';
        }
    }

    // Modern Toast Notification Helper
    function showToast(message, type = 'success') {
        let toastContainer = document.getElementById('proplink-toast-container');
        if (!toastContainer) {
            toastContainer = document.createElement('div');
            toastContainer.id = 'proplink-toast-container';
            toastContainer.className = 'toast-container position-fixed bottom-0 end-0 p-3';
            toastContainer.style.zIndex = '1090';
            document.body.appendChild(toastContainer);
        }

        const iconClass = type === 'success' ? 'bi-check-circle-fill text-success' :
                          type === 'warning' ? 'bi-exclamation-triangle-fill text-warning' :
                          'bi-info-circle-fill text-info';

        const toastEl = document.createElement('div');
        toastEl.className = 'toast align-items-center border-0 shadow-lg show';
        toastEl.setAttribute('role', 'alert');
        toastEl.setAttribute('aria-live', 'assertive');
        toastEl.setAttribute('aria-atomic', 'true');
        toastEl.innerHTML = `
            <div class="d-flex p-2 bg-white rounded-3 border">
                <div class="toast-body d-flex align-items-center gap-2 small">
                    <i class="bi ${iconClass} fs-5"></i>
                    <span>${message}</span>
                </div>
                <button type="button" class="btn-close me-2 m-auto" data-bs-dismiss="toast" aria-label="Close"></button>
            </div>
        `;

        toastContainer.appendChild(toastEl);
        setTimeout(() => {
            toastEl.remove();
        }, 4000);
    }

    // Update all UI elements across the page
    function updateAllUI() {
        const list = getCompareList();
        const count = list.length;
        const ids = list.map(i => i.id);

        // 1. Update Navbar Badges
        const navBadges = document.querySelectorAll('.nav-compare-count');
        navBadges.forEach(badge => {
            badge.textContent = count;
            if (count > 0) {
                badge.classList.remove('d-none');
            } else {
                badge.classList.add('d-none');
            }
        });

        // 2. Update all compare toggle buttons on cards and details page
        const toggleButtons = document.querySelectorAll('.btn-compare-toggle');
        toggleButtons.forEach(btn => {
            const propId = btn.getAttribute('data-property-id');
            const inCompare = isCompared(propId);

            if (inCompare) {
                btn.classList.add('btn-success', 'active');
                btn.classList.remove('btn-primary-custom', 'btn-outline-custom', 'btn-outline-secondary');
                const span = btn.querySelector('span');
                if (span) span.textContent = 'In Comparison';
                const icon = btn.querySelector('i');
                if (icon) {
                    icon.className = 'bi bi-check2-circle me-1';
                }
            } else {
                btn.classList.remove('btn-success', 'active');
                const defaultClass = btn.getAttribute('data-default-class') || 'btn-outline-custom';
                btn.classList.add(defaultClass);
                const span = btn.querySelector('span');
                if (span) span.textContent = '+ Compare';
                const icon = btn.querySelector('i');
                if (icon) {
                    icon.className = 'bi bi-plus-circle me-1';
                }
            }
        });

        // 3. Update Floating Dock
        const dock = document.getElementById('compareFloatingDock');
        const dockCount = document.getElementById('compareDockCount');
        const dockThumbs = document.getElementById('compareDockThumbnails');
        const dockActionBtn = document.getElementById('compareDockActionBtn');

        if (dock) {
            // Hide dock if currently on the compare page itself or 0 items selected
            const isComparePage = window.location.pathname.toLowerCase().includes('/compare');
            if (count > 0 && !isComparePage) {
                dock.classList.remove('d-none');
                dock.classList.add('compare-dock-visible');
            } else {
                dock.classList.add('d-none');
                dock.classList.remove('compare-dock-visible');
            }

            if (dockCount) {
                dockCount.textContent = `${count}/${MAX_COMPARE} Selected`;
            }

            if (dockActionBtn) {
                dockActionBtn.href = `/properties/compare?ids=${ids.join(',')}`;
            }

            if (dockThumbs) {
                dockThumbs.innerHTML = list.map(item => `
                    <div class="compare-dock-chip d-flex align-items-center gap-2 bg-light border rounded-pill px-2 py-1 position-relative">
                        <img src="${item.imageUrl || 'https://images.unsplash.com/photo-1600596542815-ffad4c1539a9?auto=format&fit=crop&w=1200&q=80'}" alt="${item.title}" class="rounded-circle object-fit-cover" style="width: 24px; height: 24px;" onerror="this.src='https://images.unsplash.com/photo-1600596542815-ffad4c1539a9?auto=format&fit=crop&w=1200&q=80';" />
                        <span class="small fw-semibold text-truncate" style="max-width: 100px;">${item.title}</span>
                        <button type="button" class="btn-dock-remove border-0 bg-transparent text-muted p-0 ms-1" data-remove-id="${item.id}" title="Remove">
                            <i class="bi bi-x-circle-fill"></i>
                        </button>
                    </div>
                `).join('');
            }
        }
    }

    // Initialize Comparator
    document.addEventListener('DOMContentLoaded', () => {
        // Sync URL query IDs with localStorage when on compare page
        if (window.location.pathname.toLowerCase().includes('/compare')) {
            const urlParams = new URLSearchParams(window.location.search);
            const rawIds = urlParams.get('ids');
            if (!rawIds) {
                const storedList = getCompareList();
                if (storedList && storedList.length > 0) {
                    const ids = storedList.map(i => i.id).join(',');
                    window.location.replace(`/properties/compare?ids=${ids}`);
                    return;
                }
            } else {
                const ids = rawIds.split(',').filter(id => id.trim().length > 0);
                // Also ensure existing compare list contains these IDs
                const currentList = getCompareList();
                // Read from table headers on the page to build rich local storage
                const propertyHeaders = document.querySelectorAll('.col-property-item');
                const pageItems = [];
                propertyHeaders.forEach(th => {
                    const removeBtn = th.querySelector('.btn-remove-compare');
                    if (removeBtn) {
                        const id = removeBtn.getAttribute('data-property-id');
                        const titleEl = th.querySelector('h6 a');
                        const priceEl = th.querySelector('.text-primary');
                        const imgEl = th.querySelector('img');
                        if (id) {
                            pageItems.push({
                                id: id,
                                title: titleEl ? titleEl.textContent.trim() : 'Verified Property',
                                price: priceEl ? priceEl.textContent.trim() : '',
                                imageUrl: imgEl ? imgEl.src : ''
                            });
                        }
                    }
                });

                if (pageItems.length > 0) {
                    saveCompareList(pageItems);
                }
            }
        }

        updateAllUI();

        // 1. Delegate Toggle Compare Click
        document.body.addEventListener('click', (e) => {
            const toggleBtn = e.target.closest('.btn-compare-toggle');
            if (toggleBtn) {
                e.preventDefault();
                e.stopPropagation();

                const propId = toggleBtn.getAttribute('data-property-id');
                const title = toggleBtn.getAttribute('data-property-title');
                const price = toggleBtn.getAttribute('data-property-price');
                const img = toggleBtn.getAttribute('data-property-img');
                const type = toggleBtn.getAttribute('data-property-type');
                const city = toggleBtn.getAttribute('data-property-city');

                if (isCompared(propId)) {
                    removeCompareItem(propId);
                } else {
                    addCompareItem({
                        id: propId,
                        title: title,
                        price: price,
                        imageUrl: img,
                        propertyType: type,
                        city: city
                    });
                }
            }

            // Remove from compare table header
            const removeHeaderBtn = e.target.closest('.btn-remove-compare');
            if (removeHeaderBtn) {
                e.preventDefault();
                const propId = removeHeaderBtn.getAttribute('data-property-id');
                removeCompareItem(propId);
            }

            // Remove from dock chip
            const removeDockBtn = e.target.closest('.btn-dock-remove');
            if (removeDockBtn) {
                e.preventDefault();
                const propId = removeDockBtn.getAttribute('data-remove-id');
                removeCompareItem(propId);
            }
        });

        // 2. Clear All Buttons
        const clearCompareBtn = document.getElementById('clearCompareBtn');
        if (clearCompareBtn) {
            clearCompareBtn.addEventListener('click', clearCompare);
        }

        const clearAllCompareBtn = document.getElementById('clearAllCompareBtn');
        if (clearAllCompareBtn) {
            clearAllCompareBtn.addEventListener('click', clearCompare);
        }

        // 2.1 Preference Matcher Filter & Column Focus
        const prefFilterButtons = document.querySelectorAll('.filter-pref-btn');
        const prefCardItems = document.querySelectorAll('.pref-card-item');

        prefFilterButtons.forEach(btn => {
            btn.addEventListener('click', () => {
                prefFilterButtons.forEach(b => b.classList.remove('active'));
                btn.classList.add('active');

                const filter = btn.getAttribute('data-pref-filter');
                prefCardItems.forEach(card => {
                    const cardType = card.getAttribute('data-pref-type');
                    if (filter === 'all' || cardType === filter) {
                        card.style.display = '';
                        card.classList.add('fade-in');
                    } else {
                        card.style.display = 'none';
                    }
                });
            });
        });

        // Focus and highlight property column in comparison matrix
        document.body.addEventListener('click', (e) => {
            const focusBtn = e.target.closest('.btn-focus-prop');
            if (focusBtn) {
                e.preventDefault();
                const targetId = focusBtn.getAttribute('data-target-id');
                if (targetId) {
                    const targetTh = document.querySelector(`th[data-prop-col-id="${targetId}"]`);
                    const allThs = document.querySelectorAll('.col-property-item');
                    allThs.forEach(th => th.classList.remove('prop-column-highlighted'));

                    if (targetTh) {
                        targetTh.classList.add('prop-column-highlighted');
                        targetTh.scrollIntoView({ behavior: 'smooth', block: 'center', inline: 'center' });
                        showToast('Highlighted matching property in comparison table.', 'success');
                    }
                }
            }
        });

        // 3. Highlight Differences Toggle
        const toggleDiffBtn = document.getElementById('toggleDiffBtn');
        const diffBtnText = document.getElementById('diffBtnText');
        const compareMatrixTable = document.getElementById('compareMatrixTable');

        if (toggleDiffBtn && compareMatrixTable) {
            let isDiffActive = false;
            toggleDiffBtn.addEventListener('click', () => {
                isDiffActive = !isDiffActive;
                compareMatrixTable.classList.toggle('highlight-diffs-active', isDiffActive);
                toggleDiffBtn.classList.toggle('active', isDiffActive);
                if (diffBtnText) {
                    diffBtnText.textContent = isDiffActive ? 'Show All Values' : 'Highlight Differences';
                }

                // Analyze rows
                const rows = compareMatrixTable.querySelectorAll('tbody tr.compare-row');
                rows.forEach(row => {
                    const cells = row.querySelectorAll('.compare-val');
                    if (cells.length >= 2) {
                        const values = Array.from(cells).map(c => c.textContent.replace(/\s+/g, ' ').trim());
                        const allEqual = values.every(v => v === values[0]);
                        if (allEqual) {
                            row.classList.add('row-identical');
                            row.classList.remove('row-different');
                        } else {
                            row.classList.add('row-different');
                            row.classList.remove('row-identical');
                        }
                    }
                });
            });
        }

        // 4. Share Comparison Link
        const shareCompareBtn = document.getElementById('shareCompareBtn');
        if (shareCompareBtn) {
            shareCompareBtn.addEventListener('click', () => {
                const list = getCompareList();
                const ids = list.map(i => i.id).join(',');
                const shareUrl = `${window.location.origin}/properties/compare${ids ? '?ids=' + ids : ''}`;

                if (navigator.clipboard && navigator.clipboard.writeText) {
                    navigator.clipboard.writeText(shareUrl).then(() => {
                        showToast('Shareable comparison link copied to clipboard!', 'success');
                    }).catch(() => {
                        prompt('Copy comparison link:', shareUrl);
                    });
                } else {
                    prompt('Copy comparison link:', shareUrl);
                }
            });
        }

        // 5. Quick Add Modal Live Search & Filtering
        const addPropertyModal = document.getElementById('addPropertyModal');
        const modalContainer = document.getElementById('modalPropertiesContainer');
        const modalSearchQuery = document.getElementById('modalSearchQuery');
        const modalTypeFilter = document.getElementById('modalTypeFilter');

        if (addPropertyModal && modalContainer) {
            let debounceTimer = null;

            function fetchModalProperties() {
                const q = modalSearchQuery ? modalSearchQuery.value.trim() : '';
                const type = modalTypeFilter ? modalTypeFilter.value : '';

                modalContainer.innerHTML = `
                    <div class="text-center py-4 text-muted">
                        <div class="spinner-border text-emerald spinner-border-sm mb-2" role="status"></div>
                        <p class="small mb-0">Searching verified properties...</p>
                    </div>
                `;

                const url = `/api/properties/approved-search?q=${encodeURIComponent(q)}&type=${encodeURIComponent(type)}&limit=15`;
                fetch(url)
                    .then(res => res.json())
                    .then(data => {
                        if (!data || data.length === 0) {
                            modalContainer.innerHTML = `
                                <div class="text-center py-4 text-muted">
                                    <i class="bi bi-search fs-3 d-block mb-1"></i>
                                    <p class="small mb-0">No approved properties matching your query.</p>
                                </div>
                            `;
                            return;
                        }

                        modalContainer.innerHTML = data.map(item => {
                            const alreadyAdded = isCompared(item.id);
                            return `
                                <div class="card border rounded-3 p-2 mb-1 bg-white hover-shadow transition-all">
                                    <div class="d-flex align-items-center justify-content-between gap-3">
                                        <div class="d-flex align-items-center gap-3">
                                            <img src="${item.imageUrl}" alt="${item.title}" class="rounded-3 object-fit-cover" style="width: 60px; height: 50px;" onerror="this.src='https://images.unsplash.com/photo-1600596542815-ffad4c1539a9?auto=format&fit=crop&w=1200&q=80';" />
                                            <div>
                                                <h6 class="fw-bold mb-0 text-truncate" style="max-width: 320px;">${item.title}</h6>
                                                <div class="small text-muted">
                                                    <i class="bi bi-geo-alt me-1 text-danger"></i>${item.city}, ${item.state} &bull; <span class="fw-semibold text-primary">${item.formattedPrice}</span> &bull; ${item.bedrooms} Beds, ${item.bathrooms} Baths
                                                </div>
                                            </div>
                                        </div>
                                        <button type="button" 
                                                class="btn btn-sm ${alreadyAdded ? 'btn-success' : 'btn-outline-primary-custom'} btn-compare-toggle flex-shrink-0"
                                                data-property-id="${item.id}"
                                                data-property-title="${item.title}"
                                                data-property-price="${item.formattedPrice}"
                                                data-property-img="${item.imageUrl}"
                                                data-property-type="${item.propertyType}"
                                                data-property-city="${item.city}">
                                            <i class="bi ${alreadyAdded ? 'bi-check2-circle' : 'bi-plus-circle'} me-1"></i>
                                            <span>${alreadyAdded ? 'In Comparison' : '+ Compare'}</span>
                                        </button>
                                    </div>
                                </div>
                            `;
                        }).join('');
                    })
                    .catch(err => {
                        console.error('Error fetching approved properties:', err);
                        modalContainer.innerHTML = `<div class="text-danger small text-center py-3">Failed to load listings. Please try again.</div>`;
                    });
            }

            addPropertyModal.addEventListener('show.bs.modal', () => {
                fetchModalProperties();
            });

            if (modalSearchQuery) {
                modalSearchQuery.addEventListener('input', () => {
                    clearTimeout(debounceTimer);
                    debounceTimer = setTimeout(fetchModalProperties, 250);
                });
            }

            if (modalTypeFilter) {
                modalTypeFilter.addEventListener('change', fetchModalProperties);
            }
        }
    });

    // Expose API globally
    window.PropLinkComparator = {
        getList: getCompareList,
        add: addCompareItem,
        remove: removeCompareItem,
        clear: clearCompare,
        isCompared: isCompared,
        updateUI: updateAllUI
    };
})();
