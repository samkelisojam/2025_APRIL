// ============================
// EMS Portal - JavaScript
// ============================

// --- Global Search ---
(function() {
    const input = document.getElementById('globalSearch');
    const results = document.getElementById('globalSearchResults');
    if (!input || !results) return;
    let debounce;
    input.addEventListener('input', function() {
        clearTimeout(debounce);
        const q = this.value.trim();
        if (q.length < 2) { results.classList.add('d-none'); return; }
        debounce = setTimeout(async () => {
            try {
                const res = await fetch(`/Search/Query?q=${encodeURIComponent(q)}`);
                const data = await res.json();
                if (!data.length) { results.innerHTML = '<div class="p-3 text-muted small">No results found</div>'; results.classList.remove('d-none'); return; }
                const icons = { Student:'fa-user-graduate text-success', TeamLeader:'fa-user-tie text-primary', Task:'fa-tasks text-info', Announcement:'fa-bullhorn text-warning' };
                results.innerHTML = data.map(r => `<a href="${r.url}" class="d-flex align-items-center p-2 text-decoration-none text-dark border-bottom search-result-item" style="gap:10px;"><i class="fas ${icons[r.type]||'fa-circle'} fa-sm"></i><div class="flex-grow-1"><strong class="small">${r.title}</strong><br><small class="text-muted">${r.type} ${r.subtitle ? '&middot; '+r.subtitle : ''}</small></div></a>`).join('');
                results.classList.remove('d-none');
            } catch(e) { results.classList.add('d-none'); }
        }, 300);
    });
    document.addEventListener('click', function(e) { if (!input.contains(e.target) && !results.contains(e.target)) results.classList.add('d-none'); });
    document.addEventListener('keydown', function(e) { if ((e.ctrlKey || e.metaKey) && e.key === '/') { e.preventDefault(); input.focus(); } });
})();

// --- Sidebar Toggle ---
function toggleSidebar() {
    const sidebar = document.getElementById('sidebar');
    const main = document.getElementById('mainContent');
    if (sidebar && main) {
        sidebar.classList.toggle('collapsed');
        main.classList.toggle('expanded');
        // Mobile
        if (window.innerWidth <= 768) {
            sidebar.classList.toggle('show');
        }
    }
}

// --- Loading Overlay ---
let isSaving = false;

function showLoading() {
    const overlay = document.getElementById('loadingOverlay');
    if (overlay) overlay.style.display = 'flex';
}

function hideLoading() {
    const overlay = document.getElementById('loadingOverlay');
    if (overlay) overlay.style.display = 'none';
}

// Show loading on form submit
document.addEventListener('DOMContentLoaded', function () {
    document.querySelectorAll('form:not(.no-loading)').forEach(function (form) {
        form.addEventListener('submit', function () {
            isSaving = true;
            showLoading();
            // Hide after 10s max
            setTimeout(function () { hideLoading(); isSaving = false; }, 10000);
        });
    });

    // Auto-dismiss alerts after 5s
    document.querySelectorAll('.alert-dismissible').forEach(function (alert) {
        setTimeout(function () {
            var bsAlert = bootstrap.Alert.getOrCreateInstance(alert);
            if (bsAlert) bsAlert.close();
        }, 5000);
    });

    // Init tooltips
    var tooltipTriggerList = [].slice.call(document.querySelectorAll('[data-bs-toggle="tooltip"]'));
    tooltipTriggerList.map(function (el) { return new bootstrap.Tooltip(el); });
});

// --- SignalR Connection ---
let connection = null;
let lastCheckTime = new Date().toISOString();

function initSignalR() {
    if (typeof signalR === 'undefined') return;

    connection = new signalR.HubConnectionBuilder()
        .withUrl("/notificationHub")
        .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
        .build();

    connection.on("ReceiveRefresh", function (area) {
        if (!isSaving) {
            console.log("Refresh signal received for: " + area);
            // Soft refresh - show notification instead of reloading
            showNotificationBadge();
        }
    });

    connection.on("DataChanged", function (entityType, action) {
        if (!isSaving) {
            console.log("Data changed: " + entityType + " - " + action);
            showNotificationBadge();
        }
    });

    connection.on("NewAnnouncement", function (title, message) {
        addNotification(title, message);
        showNotificationBadge();
    });

    // --- Online Users ---
    connection.on("OnlineUsersUpdated", function (users) {
        updateOnlineUsersList(users);
    });

    connection.start().then(function () {
        // Send user info after connected
        var fullName = document.getElementById('currentUserFullName')?.value || '';
        var role = document.getElementById('currentUserRole')?.value || '';
        if (fullName) {
            connection.invoke("SetUserInfo", fullName, role).catch(function () { });
        }
        // Request current list
        connection.invoke("GetOnlineUsers").then(function (users) {
            updateOnlineUsersList(users);
        }).catch(function () { });
    }).catch(function (err) {
        console.log("SignalR connection failed: " + err.toString());
    });
}

// Auto-check for updates every 60 seconds
function startAutoRefresh() {
    setInterval(function () {
        if (isSaving) return; // Don't refresh during save
        fetch('/Home/CheckUpdates?since=' + encodeURIComponent(lastCheckTime))
            .then(function (r) { return r.json(); })
            .then(function (data) {
                if (data.hasUpdates) {
                    showNotificationBadge();
                }
                lastCheckTime = new Date().toISOString();
            })
            .catch(function () { });
    }, 60000);
}

function showNotificationBadge() {
    var badge = document.getElementById('notificationBadge');
    if (badge) {
        var count = parseInt(badge.textContent || '0') + 1;
        badge.textContent = count;
        badge.style.display = 'flex';
    }
}

function addNotification(title, message) {
    var list = document.getElementById('notificationList');
    if (list) {
        var item = document.createElement('div');
        item.className = 'border-bottom pb-2 mb-2';
        item.innerHTML = '<strong class="small">' + escapeHtml(title) + '</strong><br><span class="text-muted small">' + escapeHtml(message).substring(0, 80) + '</span>';
        list.prepend(item);
    }
}

// --- SA ID Validation ---
function validateSAID(input) {
    var idNumber = input.value.trim();
    var feedbackEl = input.nextElementSibling || document.getElementById('saidFeedback');

    if (idNumber.length !== 13) {
        if (feedbackEl) {
            feedbackEl.textContent = 'SA ID must be 13 digits';
            feedbackEl.className = 'form-text text-danger';
        }
        return;
    }

    fetch('/Student/ValidateId?idNumber=' + encodeURIComponent(idNumber))
        .then(function (r) { return r.json(); })
        .then(function (data) {
            if (data.isValid) {
                if (feedbackEl) {
                    feedbackEl.innerHTML = '<i class="fas fa-check-circle text-success"></i> Valid SA ID - DOB: ' + data.dateOfBirth + ', Gender: ' + data.gender;
                    feedbackEl.className = 'form-text text-success';
                }
                // Auto-fill gender if field exists
                var genderField = document.querySelector('[name="Gender"]');
                if (genderField && data.gender) {
                    genderField.value = data.gender === 'Male' ? '0' : '1';
                }
            } else {
                if (feedbackEl) {
                    feedbackEl.innerHTML = '<i class="fas fa-times-circle text-danger"></i> ' + escapeHtml(data.errorMsg);
                    feedbackEl.className = 'form-text text-danger';
                }
            }
        })
        .catch(function () { });
}

// --- Table Export to PDF ---
function exportTableToPDF(tableId, title) {
    if (typeof jspdf === 'undefined' && typeof window.jspdf === 'undefined') {
        alert('PDF library not loaded');
        return;
    }

    var jsPDF = window.jspdf.jsPDF;
    var doc = new jsPDF('l', 'mm', 'a4');

    doc.setFontSize(16);
    doc.text(title || 'Report', 14, 15);
    doc.setFontSize(8);
    doc.text('Generated: ' + new Date().toLocaleString(), 14, 22);

    var table = document.getElementById(tableId);
    if (!table) return;

    doc.autoTable({
        html: '#' + tableId,
        startY: 28,
        styles: { fontSize: 7, cellPadding: 2 },
        headStyles: { fillColor: [13, 110, 253], textColor: 255, fontStyle: 'bold' },
        alternateRowStyles: { fillColor: [248, 250, 252] },
        margin: { top: 28 }
    });

    doc.save((title || 'report') + '_' + formatDate(new Date()) + '.pdf');
}

// --- Report Builder ---
var selectedReportFields = [];

function toggleReportField(fieldName, element) {
    var idx = selectedReportFields.indexOf(fieldName);
    if (idx > -1) {
        selectedReportFields.splice(idx, 1);
        element.classList.remove('selected');
    } else {
        selectedReportFields.push(fieldName);
        element.classList.add('selected');
    }
    updateSelectedFieldsList();
}

function updateSelectedFieldsList() {
    var container = document.getElementById('selectedFieldsList');
    if (!container) return;
    if (selectedReportFields.length === 0) {
        container.innerHTML = '<p class="text-muted small">Drag fields from the left panel</p>';
        return;
    }
    container.innerHTML = selectedReportFields.map(function (f) {
        return '<span class="badge bg-primary me-1 mb-1" style="cursor:pointer;" onclick="removeReportField(\'' + f + '\')">' + f + ' <i class="fas fa-times ms-1"></i></span>';
    }).join('');
}

function removeReportField(fieldName) {
    selectedReportFields = selectedReportFields.filter(function (f) { return f !== fieldName; });
    document.querySelectorAll('.field-item').forEach(function (el) {
        if (el.dataset.field === fieldName) el.classList.remove('selected');
    });
    updateSelectedFieldsList();
}

function runReport() {
    if (selectedReportFields.length === 0) {
        alert('Please select at least one field');
        return;
    }

    showLoading();
    var token = document.querySelector('input[name="__RequestVerificationToken"]')?.value;

    fetch('/Report/RunCustomReport', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ SelectedFields: selectedReportFields })
    })
        .then(function (r) { return r.json(); })
        .then(function (data) {
            hideLoading();
            if (data.error) { alert(data.error); return; }
            renderReportResults(data.fields, data.data);
        })
        .catch(function (err) { hideLoading(); alert('Error running report'); });
}

function renderReportResults(fields, data) {
    var container = document.getElementById('reportResults');
    if (!container) return;

    var html = '<div class="table-container"><table class="table table-hover" id="reportTable"><thead><tr>';
    fields.forEach(function (f) { html += '<th>' + escapeHtml(f) + '</th>'; });
    html += '</tr></thead><tbody>';

    data.forEach(function (row) {
        html += '<tr>';
        fields.forEach(function (f) {
            var val = row[f] != null ? row[f] : '';
            html += '<td>' + escapeHtml(String(val)) + '</td>';
        });
        html += '</tr>';
    });

    html += '</tbody></table></div>';
    html += '<div class="mt-3"><button class="btn btn-danger btn-sm me-2" onclick="exportTableToPDF(\'reportTable\', \'Custom Report\')"><i class="fas fa-file-pdf me-1"></i>Export PDF</button>';
    html += '<p class="d-inline text-muted small ms-2">Total records: ' + data.length + '</p></div>';

    container.innerHTML = html;
}

// --- Utility ---
function escapeHtml(str) {
    var div = document.createElement('div');
    div.textContent = str;
    return div.innerHTML;
}

function formatDate(date) {
    return date.getFullYear() + '' +
        String(date.getMonth() + 1).padStart(2, '0') +
        String(date.getDate()).padStart(2, '0');
}

function confirmDelete(message) {
    return confirm(message || 'Are you sure you want to delete this item?');
}

// --- Permission checkbox update ---
function updatePermission(roleName, permissionId, checkbox) {
    var row = checkbox.closest('tr');
    var data = {
        roleName: roleName,
        permissionId: permissionId,
        canView: row.querySelector('.perm-view')?.checked || false,
        canCreate: row.querySelector('.perm-create')?.checked || false,
        canEdit: row.querySelector('.perm-edit')?.checked || false,
        canDelete: row.querySelector('.perm-delete')?.checked || false
    };

    var token = document.querySelector('input[name="__RequestVerificationToken"]')?.value;
    fetch('/Settings/UpdatePermission', {
        method: 'POST',
        headers: {
            'Content-Type': 'application/x-www-form-urlencoded',
            'RequestVerificationToken': token || ''
        },
        body: new URLSearchParams(data)
    })
        .then(function (r) { return r.json(); })
        .then(function (d) {
            if (!d.success) alert('Error updating permission');
        })
        .catch(function () { alert('Error updating permission'); });
}

// --- Change user role ---
function changeUserRole(userId, selectElement) {
    var role = selectElement.value;
    var token = document.querySelector('input[name="__RequestVerificationToken"]')?.value;
    fetch('/Settings/ChangeRole', {
        method: 'POST',
        headers: {
            'Content-Type': 'application/x-www-form-urlencoded',
            'RequestVerificationToken': token || ''
        },
        body: new URLSearchParams({ userId: userId, role: role })
    })
        .then(function (r) { return r.json(); })
        .then(function (d) {
            if (d.success) {
                showToast('Role updated successfully!', 'success');
            }
        })
        .catch(function () { alert('Error changing role'); });
}

// --- Toast notification ---
function showToast(message, type) {
    var icons = { success:'fa-check-circle', danger:'fa-exclamation-circle', warning:'fa-exclamation-triangle', info:'fa-info-circle' };
    var toast = document.createElement('div');
    toast.className = 'alert alert-' + (type || 'success') + ' alert-dismissible fade show position-fixed toast-enter';
    toast.style.cssText = 'top:80px;right:20px;z-index:9999;min-width:280px;max-width:400px;box-shadow:0 8px 30px rgba(0,0,0,0.15);border-radius:12px;border:none;';
    toast.innerHTML = '<i class="fas ' + (icons[type] || icons.success) + ' me-2"></i>' + escapeHtml(message) + '<button type="button" class="btn-close" data-bs-dismiss="alert"></button>';
    document.body.appendChild(toast);
    setTimeout(function () {
        toast.style.transition = 'all 0.3s ease';
        toast.style.opacity = '0';
        toast.style.transform = 'translateX(100px)';
        setTimeout(function() { toast.remove(); }, 300);
    }, 3500);
}

// --- Task status update ---
function updateTaskStatus(taskId, newStatus) {
    var token = document.querySelector('input[name="__RequestVerificationToken"]')?.value;
    fetch('/Task/UpdateStatus', {
        method: 'POST',
        headers: {
            'Content-Type': 'application/x-www-form-urlencoded',
            'RequestVerificationToken': token || ''
        },
        body: new URLSearchParams({ id: taskId, status: newStatus })
    })
        .then(function (r) { return r.json(); })
        .then(function (d) {
            if (d.success) {
                showToast('Task status updated!', 'success');
                setTimeout(function () { location.reload(); }, 1000);
            }
        })
        .catch(function () { alert('Error updating task'); });
}

// --- Initialize on page load ---
document.addEventListener('DOMContentLoaded', function () {
    initSignalR();
    startAutoRefresh();

    // Animate stat values (count up)
    document.querySelectorAll('.stat-value').forEach(function(el) {
        var target = parseInt(el.textContent.replace(/[^0-9]/g, ''));
        if (isNaN(target) || target === 0) return;
        var prefix = el.textContent.match(/^[^0-9]*/)?.[0] || '';
        var duration = 600;
        var start = performance.now();
        function update(now) {
            var elapsed = now - start;
            var progress = Math.min(elapsed / duration, 1);
            var eased = 1 - Math.pow(1 - progress, 3); // easeOutCubic
            el.textContent = prefix + Math.round(target * eased).toLocaleString();
            if (progress < 1) requestAnimationFrame(update);
        }
        el.textContent = prefix + '0';
        requestAnimationFrame(update);
    });

    // Stagger fade-in for table rows
    document.querySelectorAll('.table tbody tr').forEach(function(tr, i) {
        tr.style.animationDelay = (i * 0.03) + 's';
        tr.classList.add('fade-in-row');
    });

    // Add slide-up to stat cards
    document.querySelectorAll('.stat-card').forEach(function(card) {
        card.classList.add('slide-up');
    });
});

// --- Online Users ---
function updateOnlineUsersList(users) {
    // Update badges
    var count = users ? users.length : 0;
    var countEl = document.getElementById('onlineCount');
    var countPanelEl = document.getElementById('onlineCountPanel');
    if (countEl) countEl.textContent = count;
    if (countPanelEl) countPanelEl.textContent = count;

    // Update list
    var listEl = document.getElementById('onlineUsersList');
    if (!listEl) return;

    if (!users || users.length === 0) {
        listEl.innerHTML = '<div class="text-center py-4 text-muted"><i class="fas fa-user-slash me-1"></i> No users online</div>';
        return;
    }

    var roleColors = {
        'Admin': 'danger',
        'Manager': 'primary',
        'TeamLeader': 'warning',
        'Staff': 'info',
        'ReadOnly': 'secondary'
    };

    var roleIcons = {
        'Admin': 'fa-user-shield',
        'Manager': 'fa-user-tie',
        'TeamLeader': 'fa-user-check',
        'Staff': 'fa-user',
        'ReadOnly': 'fa-eye'
    };

    var currentUser = document.getElementById('currentUserFullName')?.value || '';

    var html = '';
    users.forEach(function (user) {
        var color = roleColors[user.role] || 'secondary';
        var icon = roleIcons[user.role] || 'fa-user';
        var isMe = user.email === currentUser || user.fullName === currentUser;
        var connTime = user.connectedAt ? timeSince(new Date(user.connectedAt)) : '';

        html += '<div class="list-group-item d-flex align-items-center py-2' + (isMe ? ' bg-light' : '') + '">';
        html += '  <div class="position-relative me-3">';
        html += '    <div class="rounded-circle bg-' + color + ' bg-opacity-10 d-flex align-items-center justify-content-center" style="width:40px;height:40px;">';
        html += '      <i class="fas ' + icon + ' text-' + color + '"></i>';
        html += '    </div>';
        html += '    <span class="position-absolute bottom-0 end-0 bg-success rounded-circle border border-2 border-white" style="width:12px;height:12px;"></span>';
        html += '  </div>';
        html += '  <div class="flex-grow-1">';
        html += '    <div class="fw-semibold small">' + escapeHtml(user.fullName || user.email) + (isMe ? ' <span class="text-muted">(You)</span>' : '') + '</div>';
        html += '    <div class="d-flex align-items-center gap-2">';
        html += '      <span class="badge bg-' + color + '" style="font-size:0.65rem;">' + escapeHtml(user.role || 'User') + '</span>';
        if (connTime) {
            html += '      <span class="text-muted" style="font-size:0.65rem;"><i class="fas fa-clock me-1"></i>' + connTime + '</span>';
        }
        html += '    </div>';
        html += '  </div>';
        html += '</div>';
    });

    listEl.innerHTML = html;
}

function timeSince(date) {
    var seconds = Math.floor((new Date() - date) / 1000);
    if (seconds < 60) return 'Just now';
    var minutes = Math.floor(seconds / 60);
    if (minutes < 60) return minutes + 'm ago';
    var hours = Math.floor(minutes / 60);
    if (hours < 24) return hours + 'h ago';
    return Math.floor(hours / 24) + 'd ago';
}
