import psutil
import sys
import win32gui
import win32con
import win32process
import keyboard
import time

def get_windows_for_pid(pid):
	"""
	Returns a list of top-level window handles for the given process ID.
	"""
	windows = []

	def callback(hwnd, lParam):
		# Only consider visible and enabled windows
		if win32gui.IsWindowVisible(hwnd) and win32gui.IsWindowEnabled(hwnd):
			_, window_pid = win32process.GetWindowThreadProcessId(hwnd)
			if window_pid == pid:
				windows.append(hwnd)
		return True  # Continue enumeration

	win32gui.EnumWindows(callback, None)
	return windows

def find_processes(process_name):
	Robloxes = []
	for proc in psutil.process_iter(['pid', 'name']):
		if proc.info['name'] and proc.info['name'].lower() == process_name.lower():
			Robloxes.append(proc.info['pid'])

	if len(Robloxes) <= 0:
		print(f"Process '{process_name}' not found.")
	else:
		print(f"there is {len(Robloxes)} '{process_name}'.")
	return Robloxes

def send_inputs():
	keyboard.press(57)
	time.sleep(0.1)
	keyboard.release(57)

def focus_process_window(process_name):

	for pid in find_processes(process_name):
		windows = get_windows_for_pid(pid)
		if not windows:
			print(f"No window found for process with PID: {pid}.")
			continue

		for hwnd in windows:
			# Restore the window if it's minimized
			win32gui.ShowWindow(hwnd, win32con.SW_RESTORE)
			try:
				win32gui.SetForegroundWindow(hwnd)
				print(f"Sening Inputs for PID: {pid}.")
				send_inputs()
				time.sleep(0.5)
			except Exception as e:
				print(f"  Failed to focus window {hwnd} for process with PID: {pid}. Error: {e}")
			# Pause before moving to the next window

if __name__ == '__main__':
	uptime = find_processes('RBXUptimeBot.exe')

	while (len(uptime) != 0):
		print(f"MovingInstances")
		focus_process_window('RobloxPlayerBeta.exe')
		time.sleep(60) # 5 Min
		uptime = find_processes('RBXUptimeBot.exe')