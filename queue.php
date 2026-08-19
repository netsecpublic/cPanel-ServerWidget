<?php
/**
 * ============================================================================
 * SERVER STATUS MONITOR - QUEUE & LOAD ENDPOINT
 * ============================================================================
 * 
 * INSTALLATION INSTRUCTIONS:
 * 1. Upload this file (queue.php) to your web-accessible directory on the server 
 *    (e.g., public_html/queue.php or a secure subdirectory).
 * 2. Change the $secret_token below to match the "Script Auth Token" you 
 *    configure inside your desktop ServerWidget application.
 * 
 * CRONJOB SETUP (Required for Exim Mail Queue if shell_exec is blocked):
 * If your server/PHP configuration blocks shell_exec() inside web scripts, 
 * this script reads a local "queue_count.txt" file. 
 * 
 * IMPORTANT: 
 * - The "queue_count.txt" file contains ONLY the raw integer mail queue count.
 * - The cron job MUST be installed as **ROOT** (via root SSH crontab), because 
 *   standard user accounts/crons do not have the system permissions required 
 *   to query `/usr/sbin/exim -bpc`.
 * 
 * To set up the root cron job (runs every minute):
 * 1. Log into your server via SSH as root.
 * 2. Open the root crontab editor by running: crontab -e
 * 3. Add the following line (adjust the absolute path to point to your 
 *    web directory where queue_count.txt should be written):
 * 
 *    * * * * * /usr/sbin/exim -bpc > /home/username/public_html/queue_count.txt 2>&1
 * 
 * ============================================================================
 */

// 1. CONFIGURATION
$secret_token = "YOURPASSWORD123"; // Paste your exact secret Token from the widget here

// Disable all error output and disk logging
ini_set('display_errors', '0');
ini_set('log_errors', '0');
error_reporting(0);

// Basic Security Headers
header('Content-Type: application/json; charset=utf-8');
header('X-Content-Type-Options: nosniff');
header('X-Frame-Options: DENY');

// 2. AUTHENTICATION (Using the Custom Header)
$provided_token = '';
if (isset($_SERVER['HTTP_X_AUTH_TOKEN'])) {
    $provided_token = $_SERVER['HTTP_X_AUTH_TOKEN'];
} elseif (function_exists('apache_request_headers')) {
    $headers = apache_request_headers();
    if (isset($headers['X-Auth-Token'])) {
        $provided_token = $headers['X-Auth-Token'];
    }
}

// Secure timing-attack-safe comparison
if (empty($provided_token) || !hash_equals($secret_token, $provided_token)) {
    http_response_code(401);
    exit(json_encode(["status" => "error", "message" => "Unauthorized"]));
}

// 3. FETCH QUEUE (Zero disk writes, direct memory execution)
$queue_count = null;

// Primary Method: Direct execution (No Cron required if PHP allows shell_exec)
if (function_exists('shell_exec')) {
    $output = @shell_exec('/usr/sbin/exim -bpc 2>&1');
    if ($output !== null && preg_match('/^\d+$/', trim($output))) {
        $queue_count = (int)trim($output);
    }
}

// Fallback Method: Only used if your server explicitly blocks shell_exec in PHP
if ($queue_count === null && file_exists(__DIR__ . '/queue_count.txt')) {
    $content = trim(@file_get_contents(__DIR__ . '/queue_count.txt'));
    if (preg_match('/^\d+$/', $content)) {
        $queue_count = (int)$content;
    }
}

// 4. FETCH LOAD
$load = 0.0;
if (function_exists('sys_getloadavg')) {
    $load_avg = sys_getloadavg();
    $load = isset($load_avg[0]) ? round($load_avg[0], 2) : 0.0;
}

echo json_encode([
    "status" => "ok",
    "count" => $queue_count,
    "load"  => $load
]);
