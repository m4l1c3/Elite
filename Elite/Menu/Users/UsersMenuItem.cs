﻿// Author: Ryan Cobb (@cobbr_io)
// Project: Elite (https://github.com/cobbr/Elite)
// License: GNU GPLv3

using System;
using System.Linq;
using System.Collections.Generic;

using Covenant.API;
using Covenant.API.Models;

namespace Elite.Menu.Users
{
	public class MenuCommandUsersShow : MenuCommand
    {
		public MenuCommandUsersShow(CovenantAPI CovenantClient) : base(CovenantClient)
        {
            this.Name = "Show";
            this.Description = "Displays list of Covenant users.";
            this.Parameters = new List<MenuCommandParameter>();
        }
        
        public override void Command(MenuItem menuItem, string UserInput)
        {
			UsersMenuItem usersMenuItem = (UsersMenuItem)menuItem;
			usersMenuItem.Refresh();
            EliteConsoleMenu menu = new EliteConsoleMenu(EliteConsoleMenu.EliteConsoleMenuType.List, "Users");
            menu.Columns.Add("Username");
			menu.Columns.Add("Roles");
            menu.Columns.Add("Status");
			usersMenuItem.Users.ForEach(U =>
            {
				var userRoles = this.CovenantClient.ApiUsersByUidRolesGet(U.Id).ToList();
				List<string> roles = new List<string>();
				foreach (var userRole in userRoles)
				{
					IdentityRole role = CovenantClient.ApiRolesByRidGet(userRole.RoleId);
					if (role != null)
					{
						roles.Add(role.Name);
					}
				}
				menu.Rows.Add(new List<string> { U.UserName, String.Join(", ", roles), "Active" });
            });
            menu.Print();
        }
    }

	public class MenuCommandUsersCreate : MenuCommand
    {
        public MenuCommandUsersCreate(CovenantAPI CovenantClient) : base(CovenantClient)
        {
            this.Name = "Create";
            this.Description = "Create a new Covenant user.";
			this.Parameters = new List<MenuCommandParameter> {
				new MenuCommandParameter { Name = "UserName" },
				new MenuCommandParameter { Name = "Password" },
                new MenuCommandParameter {
                    Name = "Roles",
                    Values = new List<MenuCommandParameterValue> {
                        new MenuCommandParameterValue { Value = "User" },
                        new MenuCommandParameterValue { Value = "Administrator" },
                        new MenuCommandParameterValue { Value = "User,Administrator" }
                    }
                }
			};
        }

        public override void Command(MenuItem menuItem, string UserInput)
        {
            UsersMenuItem usersMenuItem = (UsersMenuItem)menuItem;
            usersMenuItem.Refresh();
			string[] commands = UserInput.Split(" ");
			if (commands.Length < 3 || commands.Length > 4 || commands[0].ToLower() != "create")
            {
                menuItem.PrintInvalidOptionError(UserInput);
				EliteConsole.PrintFormattedErrorLine("Usage: Create <username> <password> [<roles>]");
                return;
            }
			CovenantUser user = this.CovenantClient.ApiUsersPost(new CovenantUserLogin(commands[1], commands[2]));
			if (user != null)
			{
				EliteConsole.PrintFormattedHighlightLine("Created user: \"" + commands[1] + "\"");
				if (commands.Length == 4)
				{
					string[] roleNames = commands[3].Split(",");
					foreach (string roleName in roleNames)
					{
						IdentityRole role = this.CovenantClient.ApiRolesGet().FirstOrDefault(R => R.Name == roleName);
						if (role != null)
						{
							IdentityUserRoleString roleResult = this.CovenantClient.ApiUsersByUidRolesByRidPost(user.Id, role.Id);
							if (roleResult.UserId == user.Id && roleResult.RoleId == role.Id)
							{
								EliteConsole.PrintFormattedHighlightLine("Added user: \"" + commands[1] + "\"" + " to role: \"" + roleName + "\"");
							}
							else
							{
								EliteConsole.PrintFormattedErrorLine("Failed to add user: \"" + commands[1] + "\" to role: \"" + roleName + "\"");
							}
						}
					}
				}
			}
			else
			{
				EliteConsole.PrintFormattedErrorLine("Failed to create user: \"" + commands[1] + "\"");
			}
        }
    }

	public class MenuCommandUsersDelete : MenuCommand
    {
		public MenuCommandUsersDelete(CovenantAPI CovenantClient) : base(CovenantClient)
        {
            this.Name = "Delete";
            this.Description = "Delete a Covenant user.";
            this.Parameters = new List<MenuCommandParameter> {
                new MenuCommandParameter { Name = "UserName" }
            };
        }

        public override void Command(MenuItem menuItem, string UserInput)
        {
            UsersMenuItem usersMenuItem = (UsersMenuItem)menuItem;
            usersMenuItem.Refresh();
            string[] commands = UserInput.Split(" ");
            if (commands.Length != 2 || commands[0].ToLower() != "delete")
            {
                menuItem.PrintInvalidOptionError(UserInput);
                EliteConsole.PrintFormattedErrorLine("Usage: Delete <username>");
                return;
            }

			CovenantUser user = usersMenuItem.Users.FirstOrDefault(U => U.UserName == commands[1]);
            if (user != null)
			{
				EliteConsole.PrintFormattedWarning("Delete user: \"" + commands[1] + "\"? [y/N] ");
                string input = EliteConsole.Read();
                if (input.ToLower().StartsWith("y"))
                {
					this.CovenantClient.ApiUsersByUidDelete(user.Id);
                }
			}
            else
            {
                EliteConsole.PrintFormattedErrorLine("User: \"" + commands[1] + "\" does not exist.");
            }
        }
    }
    
    public class UsersMenuItem : MenuItem
    {
        public List<CovenantUser> Users { get; set; }
		public UsersMenuItem(CovenantAPI CovenantClient, EventPrinter EventPrinter) : base(CovenantClient, EventPrinter)
        {
            this.MenuTitle = "Users";
            this.MenuDescription = "Displays list of Covenant users.";
			this.AdditionalOptions.Add(new MenuCommandUsersShow(CovenantClient));
			this.AdditionalOptions.Add(new MenuCommandUsersCreate(CovenantClient));
            this.AdditionalOptions.Add(new MenuCommandUsersDelete(CovenantClient));
			this.Refresh();
        }

        public override void Refresh()
        {
            this.Users = this.CovenantClient.ApiUsersGet()
                             .Where(U => this.CovenantClient.ApiUsersByUidRolesGet(U.Id)
                             .Where(R => this.CovenantClient.ApiRolesByRidGet(R.RoleId).Name.ToLower() == "listener")
                             .Count() == 0)
                             .ToList();
            this.SetupMenuAutoComplete();
        }

        public override bool ValidateMenuParameters(string[] parameters = null, bool forwardEntrance = true)
        {
            this.Refresh();
            return true;
        }

		public override void PrintMenu()
        {
            this.AdditionalOptions.FirstOrDefault(O => O.Name == "Show").Command(this, "");
        }
    }
}
