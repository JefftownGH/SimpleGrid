using System;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace Peter;

public class ShellContextMenu : NativeWindow
{
	private struct CWPSTRUCT
	{
		public IntPtr lparam;

		public IntPtr wparam;

		public int message;

		public IntPtr hwnd;
	}

	[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
	private struct CMINVOKECOMMANDINFOEX
	{
		public int cbSize;

		public CMIC fMask;

		public IntPtr hwnd;

		public IntPtr lpVerb;

		[MarshalAs(UnmanagedType.LPStr)]
		public string lpParameters;

		[MarshalAs(UnmanagedType.LPStr)]
		public string lpDirectory;

		public SW nShow;

		public int dwHotKey;

		public IntPtr hIcon;

		[MarshalAs(UnmanagedType.LPStr)]
		public string lpTitle;

		public IntPtr lpVerbW;

		[MarshalAs(UnmanagedType.LPWStr)]
		public string lpParametersW;

		[MarshalAs(UnmanagedType.LPWStr)]
		public string lpDirectoryW;

		[MarshalAs(UnmanagedType.LPWStr)]
		public string lpTitleW;

		public POINT ptInvoke;
	}

	[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
	private struct MENUITEMINFO(string text)
	{
		public int cbSize = cbMenuItemInfo;

		public MIIM fMask = (MIIM)0u;

		public MFT fType = MFT.BYCOMMAND;

		public MFS fState = MFS.ENABLED;

		public uint wID = 0u;

		public IntPtr hSubMenu = IntPtr.Zero;

		public IntPtr hbmpChecked = IntPtr.Zero;

		public IntPtr hbmpUnchecked = IntPtr.Zero;

		public IntPtr dwItemData = IntPtr.Zero;

		[MarshalAs(UnmanagedType.LPTStr)]
		public string dwTypeData = text;

		public int cch = text.Length;

		public IntPtr hbmpItem = IntPtr.Zero;
	}

	private struct STGMEDIUM
	{
		public TYMED tymed;

		public IntPtr hBitmap;

		public IntPtr hMetaFilePict;

		public IntPtr hEnhMetaFile;

		public IntPtr hGlobal;

		public IntPtr lpszFileName;

		public IntPtr pstm;

		public IntPtr pstg;

		public IntPtr pUnkForRelease;
	}

	[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
	private struct POINT(int x, int y)
	{
		public int x = x;

		public int y = y;
	}

	[Flags]
	private enum SHGNO
	{
		NORMAL = 0,
		INFOLDER = 1,
		FOREDITING = 0x1000,
		FORADDRESSBAR = 0x4000,
		FORPARSING = 0x8000
	}

	[Flags]
	private enum SFGAO : uint
	{
		BROWSABLE = 0x8000000u,
		CANCOPY = 1u,
		CANDELETE = 0x20u,
		CANLINK = 4u,
		CANMONIKER = 0x400000u,
		CANMOVE = 2u,
		CANRENAME = 0x10u,
		CAPABILITYMASK = 0x177u,
		COMPRESSED = 0x4000000u,
		CONTENTSMASK = 0x80000000u,
		DISPLAYATTRMASK = 0xFC000u,
		DROPTARGET = 0x100u,
		ENCRYPTED = 0x2000u,
		FILESYSANCESTOR = 0x10000000u,
		FILESYSTEM = 0x40000000u,
		FOLDER = 0x20000000u,
		GHOSTED = 0x8000u,
		HASPROPSHEET = 0x40u,
		HASSTORAGE = CANMONIKER,
		HASSUBFOLDER = CONTENTSMASK,
		HIDDEN = 0x80000u,
		ISSLOW = 0x4000u,
		LINK = 0x10000u,
		NEWCONTENT = 0x200000u,
		NONENUMERATED = 0x100000u,
		READONLY = 0x40000u,
		REMOVABLE = 0x2000000u,
		SHARE = 0x20000u,
		STORAGE = 8u,
		STORAGEANCESTOR = 0x800000u,
		STORAGECAPMASK = CANMONIKER | FILESYSANCESTOR | FILESYSTEM | FOLDER | LINK | READONLY | STORAGE | STORAGEANCESTOR,
		STREAM = CANMONIKER,
		VALIDATE = 0x1000000u
	}

	[Flags]
	private enum SHCONTF
	{
		FOLDERS = 0x20,
		NONFOLDERS = 0x40,
		INCLUDEHIDDEN = 0x80,
		INIT_ON_FIRST_NEXT = 0x100,
		NETPRINTERSRCH = 0x200,
		SHAREABLE = 0x400,
		STORAGE = 0x800
	}

	[Flags]
	private enum CMF : uint
	{
		NORMAL = 0u,
		DEFAULTONLY = 1u,
		VERBSONLY = 2u,
		EXPLORE = 4u,
		NOVERBS = 8u,
		CANRENAME = 0x10u,
		NODEFAULT = 0x20u,
		INCLUDESTATIC = 0x40u,
		EXTENDEDVERBS = 0x100u,
		RESERVED = 0xFFFF0000u
	}

	[Flags]
	private enum GCS : uint
	{
		VERBA = 0u,
		HELPTEXTA = 1u,
		VALIDATEA = 2u,
		VERBW = 4u,
		HELPTEXTW = HELPTEXTA | VERBW,
		VALIDATEW = VALIDATEA | VERBW
	}

	[Flags]
	private enum TPM : uint
	{
		LEFTBUTTON = 0u,
		RIGHTBUTTON = 2u,
		LEFTALIGN = 0u,
		CENTERALIGN = 4u,
		RIGHTALIGN = 8u,
		TOPALIGN = 0u,
		VCENTERALIGN = 0x10u,
		BOTTOMALIGN = 0x20u,
		HORIZONTAL = 0u,
		VERTICAL = 0x40u,
		NONOTIFY = 0x80u,
		RETURNCMD = 0x100u,
		RECURSE = 1u,
		HORPOSANIMATION = 0x400u,
		HORNEGANIMATION = 0x800u,
		VERPOSANIMATION = 0x1000u,
		VERNEGANIMATION = 0x2000u,
		NOANIMATION = 0x4000u,
		LAYOUTRTL = 0x8000u
	}

	private enum CMD_CUSTOM
	{
		ExpandCollapse = 30001
	}

	[Flags]
	private enum CMIC : uint
	{
		HOTKEY = 0x20u,
		ICON = 0x10u,
		FLAG_NO_UI = 0x400u,
		UNICODE = 0x4000u,
		NO_CONSOLE = 0x8000u,
		ASYNCOK = 0x100000u,
		NOZONECHECKS = 0x800000u,
		SHIFT_DOWN = 0x10000000u,
		CONTROL_DOWN = 0x40000000u,
		FLAG_LOG_USAGE = 0x4000000u,
		PTINVOKE = 0x20000000u
	}

	[Flags]
	private enum SW
	{
		HIDE = 0,
		SHOWNORMAL = 1,
		NORMAL = SHOWNORMAL,
		SHOWMINIMIZED = 2,
		SHOWMAXIMIZED = SHOWNORMAL | SHOWMINIMIZED,
		MAXIMIZE = SHOWMAXIMIZED,
		SHOWNOACTIVATE = 4,
		SHOW = SHOWNORMAL | SHOWNOACTIVATE,
		MINIMIZE = SHOWMINIMIZED | SHOWNOACTIVATE,
		SHOWMINNOACTIVE = SHOWMAXIMIZED | SHOWNOACTIVATE,
		SHOWNA = 8,
		RESTORE = SHOWNORMAL | SHOWNA,
		SHOWDEFAULT = SHOWMINIMIZED | SHOWNA
	}

	[Flags]
	private enum WM : uint
	{
		ACTIVATE = 6u,
		ACTIVATEAPP = 0x1Cu,
		AFXFIRST = 0x360u,
		AFXLAST = 0x37Fu,
		APP = 0x8000u,
		ASKCBFORMATNAME = 0x30Cu,
		CANCELJOURNAL = 0x4Bu,
		CANCELMODE = 0x1Fu,
		CAPTURECHANGED = 0x215u,
		CHANGECBCHAIN = 0x30Du,
		CHAR = 0x102u,
		CHARTOITEM = 0x2Fu,
		CHILDACTIVATE = 0x22u,
		CLEAR = 0x303u,
		CLOSE = 0x10u,
		COMMAND = 0x111u,
		COMPACTING = 0x41u,
		COMPAREITEM = 0x39u,
		CONTEXTMENU = 0x7Bu,
		COPY = 0x301u,
		COPYDATA = 0x4Au,
		CREATE = 1u,
		CTLCOLORBTN = 0x135u,
		CTLCOLORDLG = 0x136u,
		CTLCOLOREDIT = 0x133u,
		CTLCOLORLISTBOX = 0x134u,
		CTLCOLORMSGBOX = 0x132u,
		CTLCOLORSCROLLBAR = 0x137u,
		CTLCOLORSTATIC = 0x138u,
		CUT = 0x300u,
		DEADCHAR = 0x103u,
		DELETEITEM = 0x2Du,
		DESTROY = 2u,
		DESTROYCLIPBOARD = 0x307u,
		DEVICECHANGE = 0x219u,
		DEVMODECHANGE = 0x1Bu,
		DISPLAYCHANGE = 0x7Eu,
		DRAWCLIPBOARD = 0x308u,
		DRAWITEM = 0x2Bu,
		DROPFILES = 0x233u,
		ENABLE = 0xAu,
		ENDSESSION = 0x16u,
		ENTERIDLE = 0x121u,
		ENTERMENULOOP = 0x211u,
		ENTERSIZEMOVE = 0x231u,
		ERASEBKGND = 0x14u,
		EXITMENULOOP = 0x212u,
		EXITSIZEMOVE = 0x232u,
		FONTCHANGE = 0x1Du,
		GETDLGCODE = 0x87u,
		GETFONT = 0x31u,
		GETHOTKEY = 0x33u,
		GETICON = 0x7Fu,
		GETMINMAXINFO = 0x24u,
		GETOBJECT = 0x3Du,
		GETSYSMENU = 0x313u,
		GETTEXT = 0xDu,
		GETTEXTLENGTH = 0xEu,
		HANDHELDFIRST = 0x358u,
		HANDHELDLAST = 0x35Fu,
		HELP = 0x53u,
		HOTKEY = 0x312u,
		HSCROLL = 0x114u,
		HSCROLLCLIPBOARD = 0x30Eu,
		ICONERASEBKGND = 0x27u,
		IME_CHAR = 0x286u,
		IME_COMPOSITION = 0x10Fu,
		IME_COMPOSITIONFULL = 0x284u,
		IME_CONTROL = 0x283u,
		IME_ENDCOMPOSITION = 0x10Eu,
		IME_KEYDOWN = 0x290u,
		IME_KEYLAST = 0x10Fu,
		IME_KEYUP = 0x291u,
		IME_NOTIFY = 0x282u,
		IME_REQUEST = 0x288u,
		IME_SELECT = IME_COMPOSITIONFULL | CREATE,
		IME_SETCONTEXT = 0x281u,
		IME_STARTCOMPOSITION = 0x10Du,
		INITDIALOG = 0x110u,
		INITMENU = 0x116u,
		INITMENUPOPUP = 0x117u,
		INPUTLANGCHANGE = 0x51u,
		INPUTLANGCHANGEREQUEST = 0x50u,
		KEYDOWN = 0x100u,
		KEYFIRST = KEYDOWN,
		KEYLAST = 0x108u,
		KEYUP = 0x101u,
		KILLFOCUS = 8u,
		LBUTTONDBLCLK = 0x203u,
		LBUTTONDOWN = 0x201u,
		LBUTTONUP = 0x202u,
		LVM_GETEDITCONTROL = 0x1018u,
		LVM_SETIMAGELIST = 0x1003u,
		MBUTTONDBLCLK = 0x209u,
		MBUTTONDOWN = 0x207u,
		MBUTTONUP = 0x208u,
		MDIACTIVATE = 0x222u,
		MDICASCADE = 0x227u,
		MDICREATE = 0x220u,
		MDIDESTROY = 0x221u,
		MDIGETACTIVE = 0x229u,
		MDIICONARRANGE = 0x228u,
		MDIMAXIMIZE = 0x225u,
		MDINEXT = 0x224u,
		MDIREFRESHMENU = 0x234u,
		MDIRESTORE = 0x223u,
		MDISETMENU = 0x230u,
		MDITILE = 0x226u,
		MEASUREITEM = 0x2Cu,
		MENUCHAR = 0x120u,
		MENUCOMMAND = 0x126u,
		MENUDRAG = 0x123u,
		MENUGETOBJECT = 0x124u,
		MENURBUTTONUP = 0x122u,
		MENUSELECT = 0x11Fu,
		MOUSEACTIVATE = 0x21u,
		MOUSEFIRST = 0x200u,
		MOUSEHOVER = 0x2A1u,
		MOUSELAST = 0x20Au,
		MOUSELEAVE = 0x2A3u,
		MOUSEMOVE = MOUSEFIRST,
		MOUSEWHEEL = 0x20Au,
		MOVE = 3u,
		MOVING = 0x216u,
		NCACTIVATE = 0x86u,
		NCCALCSIZE = 0x83u,
		NCCREATE = 0x81u,
		NCDESTROY = 0x82u,
		NCHITTEST = 0x84u,
		NCLBUTTONDBLCLK = 0xA3u,
		NCLBUTTONDOWN = 0xA1u,
		NCLBUTTONUP = 0xA2u,
		NCMBUTTONDBLCLK = NCLBUTTONDOWN | KILLFOCUS,
		NCMBUTTONDOWN = 0xA7u,
		NCMBUTTONUP = 0xA8u,
		NCMOUSEHOVER = 0x2A0u,
		NCMOUSELEAVE = 0x2A2u,
		NCMOUSEMOVE = 0xA0u,
		NCPAINT = 0x85u,
		NCRBUTTONDBLCLK = 0xA6u,
		NCRBUTTONDOWN = 0xA4u,
		NCRBUTTONUP = 0xA5u,
		NEXTDLGCTL = 0x28u,
		NEXTMENU = 0x213u,
		NOTIFY = 0x4Eu,
		NOTIFYFORMAT = 0x55u,
		NULL = 0u,
		PAINT = 0xFu,
		PAINTCLIPBOARD = 0x309u,
		PAINTICON = 0x26u,
		PALETTECHANGED = 0x311u,
		PALETTEISCHANGING = 0x310u,
		PARENTNOTIFY = 0x210u,
		PASTE = 0x302u,
		PENWINFIRST = 0x380u,
		PENWINLAST = CHANGECBCHAIN | NCDESTROY,
		POWER = 0x48u,
		PRINT = 0x317u,
		PRINTCLIENT = 0x318u,
		QUERYDRAGICON = 0x37u,
		QUERYENDSESSION = 0x11u,
		QUERYNEWPALETTE = 0x30Fu,
		QUERYOPEN = 0x13u,
		QUEUESYNC = 0x23u,
		QUIT = 0x12u,
		RBUTTONDBLCLK = 0x206u,
		RBUTTONDOWN = 0x204u,
		RBUTTONUP = 0x205u,
		RENDERALLFORMATS = 0x306u,
		RENDERFORMAT = 0x305u,
		SETCURSOR = 0x20u,
		SETFOCUS = 7u,
		SETFONT = 0x30u,
		SETHOTKEY = 0x32u,
		SETICON = 0x80u,
		SETMARGINS = HELP | SETICON,
		SETREDRAW = 0xBu,
		SETTEXT = 0xCu,
		SETTINGCHANGE = 0x1Au,
		SHOWWINDOW = 0x18u,
		SIZE = 5u,
		SIZECLIPBOARD = 0x30Bu,
		SIZING = 0x214u,
		SPOOLERSTATUS = 0x2Au,
		STYLECHANGED = 0x7Du,
		STYLECHANGING = 0x7Cu,
		SYNCPAINT = 0x88u,
		SYSCHAR = 0x106u,
		SYSCOLORCHANGE = 0x15u,
		SYSCOMMAND = 0x112u,
		SYSDEADCHAR = 0x107u,
		SYSKEYDOWN = 0x104u,
		SYSKEYUP = 0x105u,
		TCARD = 0x52u,
		TIMECHANGE = 0x1Eu,
		TIMER = 0x113u,
		TVM_GETEDITCONTROL = 0x110Fu,
		TVM_SETIMAGELIST = 0x1109u,
		UNDO = 0x304u,
		UNINITMENUPOPUP = 0x125u,
		USER = 0x400u,
		USERCHANGED = 0x54u,
		VKEYTOITEM = 0x2Eu,
		VSCROLL = 0x115u,
		VSCROLLCLIPBOARD = 0x30Au,
		WINDOWPOSCHANGED = 0x47u,
		WINDOWPOSCHANGING = 0x46u,
		WININICHANGE = 0x1Au,
		SH_NOTIFY = CREATE | USER
	}

	[Flags]
	private enum MFT : uint
	{
		GRAYED = 3u,
		DISABLED = GRAYED,
		CHECKED = 8u,
		SEPARATOR = 0x800u,
		RADIOCHECK = 0x200u,
		BITMAP = 4u,
		OWNERDRAW = 0x100u,
		MENUBARBREAK = 0x20u,
		MENUBREAK = 0x40u,
		RIGHTORDER = 0x2000u,
		BYCOMMAND = 0u,
		BYPOSITION = 0x400u,
		POPUP = 0x10u
	}

	[Flags]
	private enum MFS : uint
	{
		GRAYED = 3u,
		DISABLED = GRAYED,
		CHECKED = 8u,
		HILITE = 0x80u,
		ENABLED = 0u,
		UNCHECKED = 0u,
		UNHILITE = 0u,
		DEFAULT = 0x1000u
	}

	[Flags]
	private enum MIIM : uint
	{
		BITMAP = 0x80u,
		CHECKMARKS = 8u,
		DATA = 0x20u,
		FTYPE = 0x100u,
		ID = 2u,
		STATE = 1u,
		STRING = 0x40u,
		SUBMENU = 4u,
		TYPE = 0x10u
	}

	[Flags]
	private enum TYMED
	{
		ENHMF = 0x40,
		FILE = 2,
		GDI = 0x10,
		HGLOBAL = 1,
		ISTORAGE = 8,
		ISTREAM = 4,
		MFPICT = 0x20,
		NULL = 0
	}

	[ComImport]
	[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
	[Guid("000214E6-0000-0000-C000-000000000046")]
	private interface IShellFolder
	{
		[PreserveSig]
		int ParseDisplayName(IntPtr hwnd, IntPtr pbc, [MarshalAs(UnmanagedType.LPWStr)] string pszDisplayName, ref uint pchEaten, out IntPtr ppidl, ref SFGAO pdwAttributes);

		[PreserveSig]
		int EnumObjects(IntPtr hwnd, SHCONTF grfFlags, out IntPtr enumIDList);

		[PreserveSig]
		int BindToObject(IntPtr pidl, IntPtr pbc, ref Guid riid, out IntPtr ppv);

		[PreserveSig]
		int BindToStorage(IntPtr pidl, IntPtr pbc, ref Guid riid, out IntPtr ppv);

		[PreserveSig]
		int CompareIDs(IntPtr lParam, IntPtr pidl1, IntPtr pidl2);

		[PreserveSig]
		int CreateViewObject(IntPtr hwndOwner, Guid riid, out IntPtr ppv);

		[PreserveSig]
		int GetAttributesOf(uint cidl, [MarshalAs(UnmanagedType.LPArray)] IntPtr[] apidl, ref SFGAO rgfInOut);

		[PreserveSig]
		int GetUIObjectOf(IntPtr hwndOwner, uint cidl, [MarshalAs(UnmanagedType.LPArray)] IntPtr[] apidl, ref Guid riid, IntPtr rgfReserved, out IntPtr ppv);

		[PreserveSig]
		int GetDisplayNameOf(IntPtr pidl, SHGNO uFlags, IntPtr lpName);

		[PreserveSig]
		int SetNameOf(IntPtr hwnd, IntPtr pidl, [MarshalAs(UnmanagedType.LPWStr)] string pszName, SHGNO uFlags, out IntPtr ppidlOut);
	}

	[ComImport]
	[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
	[Guid("000214e4-0000-0000-c000-000000000046")]
	private interface IContextMenu
	{
		[PreserveSig]
		int QueryContextMenu(IntPtr hmenu, uint iMenu, uint idCmdFirst, uint idCmdLast, CMF uFlags);

		[PreserveSig]
		int InvokeCommand(ref CMINVOKECOMMANDINFOEX info);

		[PreserveSig]
		int GetCommandString(uint idcmd, GCS uflags, uint reserved, [MarshalAs(UnmanagedType.LPArray)] byte[] commandstring, int cch);
	}

	[ComImport]
	[Guid("000214f4-0000-0000-c000-000000000046")]
	[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
	private interface IContextMenu2
	{
		[PreserveSig]
		int QueryContextMenu(IntPtr hmenu, uint iMenu, uint idCmdFirst, uint idCmdLast, CMF uFlags);

		[PreserveSig]
		int InvokeCommand(ref CMINVOKECOMMANDINFOEX info);

		[PreserveSig]
		int GetCommandString(uint idcmd, GCS uflags, uint reserved, [MarshalAs(UnmanagedType.LPWStr)] StringBuilder commandstring, int cch);

		[PreserveSig]
		int HandleMenuMsg(uint uMsg, IntPtr wParam, IntPtr lParam);
	}

	[ComImport]
	[Guid("bcfce0a0-ec17-11d0-8d10-00a0c90f2719")]
	[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
	private interface IContextMenu3
	{
		[PreserveSig]
		int QueryContextMenu(IntPtr hmenu, uint iMenu, uint idCmdFirst, uint idCmdLast, CMF uFlags);

		[PreserveSig]
		int InvokeCommand(ref CMINVOKECOMMANDINFOEX info);

		[PreserveSig]
		int GetCommandString(uint idcmd, GCS uflags, uint reserved, [MarshalAs(UnmanagedType.LPWStr)] StringBuilder commandstring, int cch);

		[PreserveSig]
		int HandleMenuMsg(uint uMsg, IntPtr wParam, IntPtr lParam);

		[PreserveSig]
		int HandleMenuMsg2(uint uMsg, IntPtr wParam, IntPtr lParam, IntPtr plResult);
	}

	private IContextMenu _oContextMenu;

	private IContextMenu2 _oContextMenu2;

	private IContextMenu3 _oContextMenu3;

	private IShellFolder _oDesktopFolder;

	private IShellFolder _oParentFolder;

	private IntPtr[] _arrPIDLs;

	private string _strParentFolder;

	private const int MAX_PATH = 260;

	private const uint CMD_FIRST = 1u;

	private const uint CMD_LAST = 30000u;

	private const int S_OK = 0;

	private const int S_FALSE = 1;

	private static int cbMenuItemInfo = Marshal.SizeOf(typeof(MENUITEMINFO));

	private static int cbInvokeCommand = Marshal.SizeOf(typeof(CMINVOKECOMMANDINFOEX));

	private static Guid IID_IShellFolder = new Guid("{000214E6-0000-0000-C000-000000000046}");

	private static Guid IID_IContextMenu = new Guid("{000214e4-0000-0000-c000-000000000046}");

	private static Guid IID_IContextMenu2 = new Guid("{000214f4-0000-0000-c000-000000000046}");

	private static Guid IID_IContextMenu3 = new Guid("{bcfce0a0-ec17-11d0-8d10-00a0c90f2719}");

	public ShellContextMenu()
	{
		CreateHandle(new CreateParams());
	}

	~ShellContextMenu()
	{
		ReleaseAll();
	}

	private bool GetContextMenuInterfaces(IShellFolder oParentFolder, IntPtr[] arrPIDLs, out IntPtr ctxMenuPtr)
	{
		if (oParentFolder.GetUIObjectOf(IntPtr.Zero, (uint)arrPIDLs.Length, arrPIDLs, ref IID_IContextMenu, IntPtr.Zero, out ctxMenuPtr) == 0)
		{
			_oContextMenu = (IContextMenu)Marshal.GetTypedObjectForIUnknown(ctxMenuPtr, typeof(IContextMenu));
			return true;
		}
		ctxMenuPtr = IntPtr.Zero;
		_oContextMenu = null;
		return false;
	}

	protected override void WndProc(ref Message m)
	{
		if (_oContextMenu != null && m.Msg == 287 && (ShellHelper.HiWord(m.WParam) & 0x800) == 0 && (ShellHelper.HiWord(m.WParam) & 0x10) == 0)
		{
			_ = string.Empty;
			ShellHelper.LoWord(m.WParam);
			_ = 30001;
		}
		if ((_oContextMenu2 == null || (m.Msg != 279 && m.Msg != 44 && m.Msg != 43) || _oContextMenu2.HandleMenuMsg((uint)m.Msg, m.WParam, m.LParam) != 0) && (_oContextMenu3 == null || m.Msg != 288 || _oContextMenu3.HandleMenuMsg2((uint)m.Msg, m.WParam, m.LParam, IntPtr.Zero) != 0))
		{
			base.WndProc(ref m);
		}
	}

	private void InvokeCommand(IContextMenu oContextMenu, uint nCmd, string strFolder, Point pointInvoke)
	{
		CMINVOKECOMMANDINFOEX info = new CMINVOKECOMMANDINFOEX
		{
			cbSize = cbInvokeCommand,
			lpVerb = (IntPtr)(nCmd - 1),
			lpDirectory = strFolder,
			lpVerbW = (IntPtr)(nCmd - 1),
			lpDirectoryW = strFolder,
			fMask = (CMIC)(0x20004000 | (((Control.ModifierKeys & Keys.Control) != Keys.None) ? 1073741824 : 0) | (((Control.ModifierKeys & Keys.Shift) != Keys.None) ? 268435456 : 0)),
			ptInvoke = new POINT(pointInvoke.X, pointInvoke.Y),
			nShow = SW.SHOWNORMAL
		};
		oContextMenu.InvokeCommand(ref info);
	}

	private void ReleaseAll()
	{
		if (_oContextMenu != null)
		{
			Marshal.ReleaseComObject(_oContextMenu);
			_oContextMenu = null;
		}
		if (_oContextMenu2 != null)
		{
			Marshal.ReleaseComObject(_oContextMenu2);
			_oContextMenu2 = null;
		}
		if (_oContextMenu3 != null)
		{
			Marshal.ReleaseComObject(_oContextMenu3);
			_oContextMenu3 = null;
		}
		if (_oDesktopFolder != null)
		{
			Marshal.ReleaseComObject(_oDesktopFolder);
			_oDesktopFolder = null;
		}
		if (_oParentFolder != null)
		{
			Marshal.ReleaseComObject(_oParentFolder);
			_oParentFolder = null;
		}
		if (_arrPIDLs != null)
		{
			FreePIDLs(_arrPIDLs);
			_arrPIDLs = null;
		}
	}

	private IShellFolder GetDesktopFolder()
	{
		IntPtr ppshf = IntPtr.Zero;
		if (_oDesktopFolder == null)
		{
			if (SHGetDesktopFolder(out ppshf) != 0)
			{
				throw new ShellContextMenuException("Failed to get the desktop shell folder");
			}
			_oDesktopFolder = (IShellFolder)Marshal.GetTypedObjectForIUnknown(ppshf, typeof(IShellFolder));
		}
		return _oDesktopFolder;
	}

	private IShellFolder GetParentFolder(string folderName)
	{
		if (_oParentFolder == null)
		{
			IShellFolder desktopFolder = GetDesktopFolder();
			if (desktopFolder == null)
			{
				return null;
			}
			IntPtr ppidl = IntPtr.Zero;
			uint pchEaten = 0u;
			SFGAO pdwAttributes = (SFGAO)0u;
			if (desktopFolder.ParseDisplayName(IntPtr.Zero, IntPtr.Zero, folderName, ref pchEaten, out ppidl, ref pdwAttributes) != 0)
			{
				return null;
			}
			IntPtr intPtr = Marshal.AllocCoTaskMem(524);
			Marshal.WriteInt32(intPtr, 0, 0);
			int displayNameOf = _oDesktopFolder.GetDisplayNameOf(ppidl, SHGNO.FORPARSING, intPtr);
			StringBuilder stringBuilder = new StringBuilder(260);
			StrRetToBuf(intPtr, ppidl, stringBuilder, 260);
			Marshal.FreeCoTaskMem(intPtr);
			intPtr = IntPtr.Zero;
			_strParentFolder = stringBuilder.ToString();
			IntPtr ppv = IntPtr.Zero;
			displayNameOf = desktopFolder.BindToObject(ppidl, IntPtr.Zero, ref IID_IShellFolder, out ppv);
			Marshal.FreeCoTaskMem(ppidl);
			if (displayNameOf != 0)
			{
				return null;
			}
			_oParentFolder = (IShellFolder)Marshal.GetTypedObjectForIUnknown(ppv, typeof(IShellFolder));
		}
		return _oParentFolder;
	}

	protected IntPtr[] GetPIDLs(FileInfo[] arrFI)
	{
		if (arrFI == null || arrFI.Length == 0)
		{
			return null;
		}
		IShellFolder parentFolder = GetParentFolder(arrFI[0].DirectoryName);
		if (parentFolder == null)
		{
			return null;
		}
		IntPtr[] array = new IntPtr[arrFI.Length];
		int num = 0;
		foreach (FileInfo fileInfo in arrFI)
		{
			uint pchEaten = 0u;
			SFGAO pdwAttributes = (SFGAO)0u;
			IntPtr ppidl = IntPtr.Zero;
			if (parentFolder.ParseDisplayName(IntPtr.Zero, IntPtr.Zero, fileInfo.Name, ref pchEaten, out ppidl, ref pdwAttributes) != 0)
			{
				FreePIDLs(array);
				return null;
			}
			array[num] = ppidl;
			num++;
		}
		return array;
	}

	protected IntPtr[] GetPIDLs(DirectoryInfo[] arrFI)
	{
		if (arrFI == null || arrFI.Length == 0)
		{
			return null;
		}
		IShellFolder parentFolder = GetParentFolder(arrFI[0].Parent.FullName);
		if (parentFolder == null)
		{
			return null;
		}
		IntPtr[] array = new IntPtr[arrFI.Length];
		int num = 0;
		foreach (DirectoryInfo directoryInfo in arrFI)
		{
			uint pchEaten = 0u;
			SFGAO pdwAttributes = (SFGAO)0u;
			IntPtr ppidl = IntPtr.Zero;
			if (parentFolder.ParseDisplayName(IntPtr.Zero, IntPtr.Zero, directoryInfo.Name, ref pchEaten, out ppidl, ref pdwAttributes) != 0)
			{
				FreePIDLs(array);
				return null;
			}
			array[num] = ppidl;
			num++;
		}
		return array;
	}

	protected void FreePIDLs(IntPtr[] arrPIDLs)
	{
		if (arrPIDLs == null)
		{
			return;
		}
		for (int i = 0; i < arrPIDLs.Length; i++)
		{
			if (arrPIDLs[i] != IntPtr.Zero)
			{
				Marshal.FreeCoTaskMem(arrPIDLs[i]);
				arrPIDLs[i] = IntPtr.Zero;
			}
		}
	}

	private void InvokeContextMenuDefault(FileInfo[] arrFI)
	{
		ReleaseAll();
		IntPtr intPtr = IntPtr.Zero;
		IntPtr ctxMenuPtr = IntPtr.Zero;
		try
		{
			_arrPIDLs = GetPIDLs(arrFI);
			if (_arrPIDLs == null)
			{
				ReleaseAll();
				return;
			}
			if (!GetContextMenuInterfaces(_oParentFolder, _arrPIDLs, out ctxMenuPtr))
			{
				ReleaseAll();
				return;
			}
			intPtr = CreatePopupMenu();
			_oContextMenu.QueryContextMenu(intPtr, 0u, 1u, 30000u, (CMF)(1 | (((Control.ModifierKeys & Keys.Shift) != Keys.None) ? 256 : 0)));
			uint menuDefaultItem = (uint)GetMenuDefaultItem(intPtr, fByPos: false, 0u);
			if (menuDefaultItem >= 1)
			{
				InvokeCommand(_oContextMenu, menuDefaultItem, arrFI[0].DirectoryName, Control.MousePosition);
			}
			DestroyMenu(intPtr);
			intPtr = IntPtr.Zero;
		}
		catch
		{
			throw;
		}
		finally
		{
			if (intPtr != IntPtr.Zero)
			{
				DestroyMenu(intPtr);
			}
			ReleaseAll();
		}
	}

	public void ShowContextMenu(FileInfo[] files, Point pointScreen)
	{
		ReleaseAll();
		_arrPIDLs = GetPIDLs(files);
		ShowContextMenu(pointScreen);
	}

	public void ShowContextMenu(DirectoryInfo[] dirs, Point pointScreen)
	{
		ReleaseAll();
		_arrPIDLs = GetPIDLs(dirs);
		ShowContextMenu(pointScreen);
	}

	private void ShowContextMenu(Point pointScreen)
	{
		IntPtr intPtr = IntPtr.Zero;
		IntPtr ctxMenuPtr = IntPtr.Zero;
		IntPtr ppv = IntPtr.Zero;
		IntPtr ppv2 = IntPtr.Zero;
		try
		{
			if (_arrPIDLs == null)
			{
				ReleaseAll();
				return;
			}
			if (!GetContextMenuInterfaces(_oParentFolder, _arrPIDLs, out ctxMenuPtr))
			{
				ReleaseAll();
				return;
			}
			intPtr = CreatePopupMenu();
			_oContextMenu.QueryContextMenu(intPtr, 0u, 1u, 30000u, (CMF)(4 | (((Control.ModifierKeys & Keys.Shift) != Keys.None) ? 256 : 0)));
			Marshal.QueryInterface(ctxMenuPtr, ref IID_IContextMenu2, out ppv);
			Marshal.QueryInterface(ctxMenuPtr, ref IID_IContextMenu3, out ppv2);
			_oContextMenu2 = (IContextMenu2)Marshal.GetTypedObjectForIUnknown(ppv, typeof(IContextMenu2));
			_oContextMenu3 = (IContextMenu3)Marshal.GetTypedObjectForIUnknown(ppv2, typeof(IContextMenu3));
			uint num = TrackPopupMenuEx(intPtr, TPM.RETURNCMD, pointScreen.X, pointScreen.Y, base.Handle, IntPtr.Zero);
			DestroyMenu(intPtr);
			intPtr = IntPtr.Zero;
			if (num != 0)
			{
				InvokeCommand(_oContextMenu, num, _strParentFolder, pointScreen);
			}
		}
		catch
		{
			throw;
		}
		finally
		{
			if (intPtr != IntPtr.Zero)
			{
				DestroyMenu(intPtr);
			}
			if (ctxMenuPtr != IntPtr.Zero)
			{
				Marshal.Release(ctxMenuPtr);
			}
			if (ppv != IntPtr.Zero)
			{
				Marshal.Release(ppv);
			}
			if (ppv2 != IntPtr.Zero)
			{
				Marshal.Release(ppv2);
			}
			ReleaseAll();
		}
	}

	[DllImport("shell32.dll")]
	private static extern int SHGetDesktopFolder(out IntPtr ppshf);

	[DllImport("shlwapi.dll", CharSet = CharSet.Auto, SetLastError = true)]
	private static extern int StrRetToBuf(IntPtr pstr, IntPtr pidl, StringBuilder pszBuf, int cchBuf);

	[DllImport("user32.dll", CharSet = CharSet.Auto, ExactSpelling = true)]
	private static extern uint TrackPopupMenuEx(IntPtr hmenu, TPM flags, int x, int y, IntPtr hwnd, IntPtr lptpm);

	[DllImport("user32", CharSet = CharSet.Auto, SetLastError = true)]
	private static extern IntPtr CreatePopupMenu();

	[DllImport("user32", CharSet = CharSet.Auto, SetLastError = true)]
	private static extern bool DestroyMenu(IntPtr hMenu);

	[DllImport("user32", CharSet = CharSet.Auto, SetLastError = true)]
	private static extern int GetMenuDefaultItem(IntPtr hMenu, bool fByPos, uint gmdiFlags);
}
