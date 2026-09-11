/*
 * Poison Menu  PluginInfo.cs
 * A community driven mod menu for Gorilla Tag with over 1000+ mods
 *
 * Copyright (C) 2026  Poison Software
 * https://github.com/heycanihavethis/Poison
 * 
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 * 
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 * 
 * You should have received a copy of the GNU General Public License
 * along with this program.  If not, see <https://www.gnu.org/licenses/>.
 */

namespace Poison
{
    public class PluginInfo
    {
        public const string GUID = "org.Poison.gorillatag.Poisonmenu";
        public const string Name = "Poison Menu";
        public const string Description = "Community powered mod menu for Gorilla Tag.";
        public const string BuildTimestamp = "2026-09-11T01:50:32Z";
        public const string Version = "5.0.2";

        public const string BaseDirectory =
#if LEGAL || LEGAL_DEBUG
            "PoisonMenu/Legal";
#else
            "PoisonMenu";
#endif
        public const string ClientResourcePath = "PoisonMenu.Resources.Client";
        public const string ServerResourcePath = "https://raw.githubusercontent.com/heycanihavethis/Poison/master/Resources/Server";
        public const string ServerAPI = "https://menu.Poison.software";
        public const string Logo = @"
                                            %%%%%                                                   
                                           %%% %%%%                                                 
                                         %%%      %%%%                                              
                                        %%%         %%%%        %%%  %                              
                                      %%%%            %%%%%%%% %%%%  %%                             
                                     %%%        %#####% %%%%%        %%                             
                                    %%%       ############ %%%                                      
                                  %%%       ######     %###  %%%%     %%%                           
                                %%%%       ######        ###   %#%%    %%                           
                             %%%#%        ######         ###%    %#%%                               
                       %%%%  %%#%         ######         %###      %##% %%                          
                 %%%%  %%   %##           ######%         ##%         %###%                         
                           %#%             ######        ###            ###%                        
                         %##%              %######%    #####              ###%                      
#%   %##                  #######%                        ###                    
                   %% %##                     %#######%                        ###%                 
###                        %########%                       ###%               
###                            %#######%                       %##%             
                  %##                                %#######%                        ###           
                %##%                                   %#######%                     ###%           
###                   %##########%        #######%                   ###             
##%                  %####%    %####        %######%                ###               
###                  %###%        %##%         %######%              ###                
###                 ###%          %%%           %######%            ##%                 
###              %###                          #######          ####                  
                %###           ####                          #######        %###                    
####         ####                          #######       ###   ##                 
                    %###       ####                         %######       ##%    ##%                
###      ###                         ######      ###                         
                         %###   ####                       ######      ###        %%%               
####  %####                   %######     ###           #%               
                            %%###% ####%              ########      ##%         %%%                 
###%%######%%    %#########%      ###     %%%% %%%%                 
                             %#   %### %###############%         ##%%%%% %%%%                       
                              %%    %##%                       %##  %                               
                                       %##                    %#%                                   
                               %%        %#%%               %%%%                                    
                               %%%         %%#%            %%%                                      
                                      %%%%%  %%%%        %%%%                                       
                                 %%%%           %%%     %%%                                         
                                                  %%%% %%%                                          
                                                    %%%%                                            ";

#if DEBUG || LEGAL_DEBUG
        public static bool BetaBuild = true;
#else
        public static bool BetaBuild = false;
#endif
    }
}
