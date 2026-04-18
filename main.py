import tkinter as tk
from app import EtsParserApp


def main():
    root = tk.Tk()
    EtsParserApp(root)
    root.mainloop()


if __name__ == "__main__":
    main()